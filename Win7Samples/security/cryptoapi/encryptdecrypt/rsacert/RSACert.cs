using Vanara.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.AdvApi32;
using static Vanara.PInvoke.Crypt32;
using static Vanara.PInvoke.Kernel32;

const int IN_BUFFER_SIZE    = 64 * 1024;

// OUT_BUFFER_SIZE is 8 bytes larger than IN_BUFFER_SIZE
// When CALG_RC2 algorithm is used, encrypted data 
// will be 8 bytes larger than IN_BUFFER_SIZE
const int OUT_BUFFER_SIZE   = IN_BUFFER_SIZE + 8; // extra padding

const CertEncodingType MY_ENCODING = CertEncodingType.X509_ASN_ENCODING | CertEncodingType.PKCS_7_ASN_ENCODING;

if (args.Length != 6)
{
	PrintUsage();
	return;
}

/* Check whether the action to be performed is encrypt or decrypt */
bool fEncrypt;
if (string.Compare(args[0], "/e", StringComparison.OrdinalIgnoreCase) == 0)
{
	fEncrypt = true;
}
else if (string.Compare(args[0], "/d", StringComparison.OrdinalIgnoreCase) == 0)
{
	fEncrypt = false;
}
else
{
	PrintUsage();
	return;
}

string lpszCertificateName = args[1];
string lpszCertificateStoreName = args[2];

/* Check whether the certificate store to be opened is user or machine */
CertStoreFlags dwCertStoreOpenFlags;
if (string.Compare(args[3], "/u", StringComparison.OrdinalIgnoreCase) == 0)
{
	dwCertStoreOpenFlags = CertStoreFlags.CERT_SYSTEM_STORE_CURRENT_USER;
}
else if (string.Compare(args[3], "/m", StringComparison.OrdinalIgnoreCase) == 0)
{
	dwCertStoreOpenFlags = CertStoreFlags.CERT_SYSTEM_STORE_LOCAL_MACHINE;
}
else
{
	PrintUsage();
	return;
}

string lpszInputFileName = args[4];
string lpszOutputFileName = args[5];

EncryptDecryptFile(lpszCertificateName,
				   lpszCertificateStoreName,
				   dwCertStoreOpenFlags,
				   lpszInputFileName,
				   lpszOutputFileName,
				   fEncrypt);

void PrintUsage()
{
   MyPrintf("RSACert [</e>|</d>] <CertName> <StoreName> [</u>|</m>] <InputFile> <OutputFile>\n");
   MyPrintf("/e for Encryption\n");
   MyPrintf("/d for Decryption\n");
   MyPrintf("/u for current user certificate store\n");
   MyPrintf("/m for local machine certificate store\n");
}

void MyPrintf(string lpszFormat, params object?[]? args) => Console.Write(lpszFormat, args);

SafePCCERT_CONTEXT GetCertificateContextFromName(string? certificateName, string? certificateStoreName, CertStoreFlags certStoreOpenFlags)
{
	// Open the specified certificate store
	using var hCertStore = CertOpenStore("MY", 0, default, CertStoreFlags.CERT_STORE_READONLY_FLAG | certStoreOpenFlags, certificateStoreName);

	if (hCertStore.IsInvalid)
	{
		Console.WriteLine($"CertOpenStore failed with {Marshal.GetLastWin32Error():X}");
		return SafePCCERT_CONTEXT.Null;
	}

	// Find the certificate by CN.
	var pCertContext = CertFindCertificateInStore(hCertStore, MY_ENCODING, 0, CertFindType.CERT_FIND_SUBJECT_STR, certificateName, default);

	if (pCertContext.IsInvalid)
	{
		Console.WriteLine($"CertFindCertificateInStore failed with {Marshal.GetLastWin32Error():X}");
	}

	return pCertContext;
}

bool AcquireAndGetRSAKey(SafePCCERT_CONTEXT cert, out SafeHCRYPTPROV phProv, out SafeHCRYPTKEY phRSAKey, bool fEncrypt, out bool pfCallerFreeProv)
{
	bool fSuccess = false;
	CertKeySpec dwKeySpec = 0;

	phProv = SafeHCRYPTPROV.Null;
	phRSAKey = SafeHCRYPTKEY.Null;
	pfCallerFreeProv = false;

	try
	{
		if (fEncrypt)
		{
			// Acquire context for RSA key
			fSuccess = CryptAcquireContext(out phProv, null, CryptProviderName.MS_DEF_PROV, PROV_RSA_FULL, CryptAcquireContextFlags.CRYPT_VERIFYCONTEXT);
			if (!fSuccess)
			{
				Console.WriteLine($"CryptAcquireContext failed with {Marshal.GetLastWin32Error()}");
				return false;
			}

			// Import the RSA public key from the certificate context
			unsafe
			{
				fSuccess = CryptImportPublicKeyInfo(phProv, MY_ENCODING, cert.AsRef().pCertInfo.AsRef().SubjectPublicKeyInfo, out phRSAKey);
				if (!fSuccess)
				{
					Console.WriteLine($"CryptImportPublicKeyInfo failed with {Marshal.GetLastWin32Error()}");
					return false;
				}
			}

			pfCallerFreeProv = true;
		}
		else
		{
			// Acquire the RSA private key
			fSuccess = CryptAcquireCertificatePrivateKey(cert,
				CryptAcquireFlags.CRYPT_ACQUIRE_USE_PROV_INFO_FLAG | CryptAcquireFlags.CRYPT_ACQUIRE_COMPARE_KEY_FLAG,
				default, out var h, out dwKeySpec, out pfCallerFreeProv);
			if (!fSuccess)
			{
				Console.WriteLine($"CryptAcquireCertificatePrivateKey failed with {Marshal.GetLastWin32Error()}");
				return false;
			}
			phProv = new(h);

			// Get the RSA key handle
			fSuccess = CryptGetUserKey(phProv, dwKeySpec, out phRSAKey);
			if (!fSuccess)
			{
				Console.WriteLine($"CryptGetUserKey failed with {Marshal.GetLastWin32Error()}");
				return false;
			}
		}
	}
	finally
	{
		if (!fSuccess)
		{
			phProv.Dispose();
			phRSAKey.Dispose();
		}
	}

	return fSuccess;
}

bool EncryptDecryptFile(string lpszCertificateName, string lpszCertificateStoreName, CertStoreFlags dwCertStoreOpenFlags,
	string lpszInputFileName, string lpszOutputFileName, bool fEncrypt)
{
	SafeHCRYPTKEY hSessionKey;
	byte[] pbBuffer = new byte[OUT_BUFFER_SIZE];
	bool fSuccess = false;

	using SafePCCERT_CONTEXT pCertContext = GetCertificateContextFromName(lpszCertificateName, lpszCertificateStoreName, dwCertStoreOpenFlags);
	if (!pCertContext)
	{
		return false;
	}

	var fResult = AcquireAndGetRSAKey(pCertContext, out var hProv, out var hRSAKey, fEncrypt, out var fCallerFreeProv);
	if (fResult == false)
	{
		return false;
	}

	// Open the input file to be encrypted or decrypted
	using var hInFile = CreateFile(lpszInputFileName, FileAccess.GENERIC_READ, 0, default, CreationOption.OPEN_EXISTING,
		FileFlagsAndAttributes.FILE_ATTRIBUTE_NORMAL, default);
	if (!hInFile)
	{
		MyPrintf("CreateFile failed with {0}\n", Marshal.GetLastWin32Error());
		return false;
	}

	// Open the output file to write the encrypted or decrypted data
	using var hOutFile = CreateFile(lpszOutputFileName, FileAccess.GENERIC_WRITE, 0, default, CreationOption.CREATE_ALWAYS,
		FileFlagsAndAttributes.FILE_ATTRIBUTE_NORMAL, default);
	if (!hOutFile)
	{
		MyPrintf("CreateFile failed with {0}\n", Marshal.GetLastWin32Error());
		return false;
	}

	if (fEncrypt)
	{
		fResult = CryptGenKey(hProv, ALG_ID.CALG_RC4, CryptGenKeyFlags.CRYPT_EXPORTABLE, out hSessionKey);
		if (!fResult)
		{
			MyPrintf("CryptGenKey failed with {0:X}\n", Marshal.GetLastWin32Error());
			return false;
		}

		// The first call to ExportKey with default gets the key size.
		uint dwSessionKeyBlob = 0;
		fResult = CryptExportKey(hSessionKey, hRSAKey, BlobType.SIMPLEBLOB, 0, null, ref dwSessionKeyBlob);
		if (!fResult)
		{
			MyPrintf("CryptExportKey failed with {0:X}\n", Marshal.GetLastWin32Error());
			return false;
		}

		// Allocate memory for Encrypted Session key blob
		using SafeLocalHandle pbSessionKeyBlob = new(dwSessionKeyBlob);
		if (!pbSessionKeyBlob)
		{
			MyPrintf("LocalAlloc failed with {0}\n", Marshal.GetLastWin32Error());
			return false;
		}

		fResult = CryptExportKey(hSessionKey, hRSAKey, BlobType.SIMPLEBLOB, 0, pbSessionKeyBlob, ref dwSessionKeyBlob);
		if (!fResult)
		{
			MyPrintf("CryptExportKey failed with {0:X}\n", Marshal.GetLastWin32Error());
			return false;
		}

		// Write the size of key blob, then the key blob itself, to output file.
		using PinnedObject pdwSessionKeyBlob = new(dwSessionKeyBlob);
		fResult = WriteFile(hOutFile, pdwSessionKeyBlob, sizeof(uint), out var dwBytesWritten, default);
		if (!fResult)
		{
			MyPrintf("WriteFile failed with {0}\n", Marshal.GetLastWin32Error());
			return false;
		}
		fResult = WriteFile(hOutFile, pbSessionKeyBlob, dwSessionKeyBlob, out dwBytesWritten, default);
		if (!fResult)
		{
			MyPrintf("WriteFile failed with {0}\n", Marshal.GetLastWin32Error());
			return false;
		}
	}
	else
	{
		// Read in key block size, then key blob itself from input file.
		fResult = ReadFile(hInFile, out uint dwByteCount);
		if (!fResult)
		{
			MyPrintf("ReadFile failed with {0}\n", Marshal.GetLastWin32Error());
			return false;
		}

		fResult = ReadFile(hInFile, pbBuffer, dwByteCount, out _, default);
		if (!fResult)
		{
			MyPrintf("ReadFile failed with {0}\n", Marshal.GetLastWin32Error());
			return false;
		}

		// import key blob into "CSP"
		fResult = CryptImportKey(hProv, pbBuffer, (int)dwByteCount, hRSAKey, 0, out hSessionKey);
		if (!fResult)
		{
			MyPrintf("CryptImportKey failed with {0:X}\n", Marshal.GetLastWin32Error());
			return false;
		}
	}

	bool finished = false;
	do
	{
		// Now read data from the input file 64K bytes at a time.
		fResult = ReadFile(hInFile, pbBuffer, IN_BUFFER_SIZE, out var dwByteCount, default);

		// If the file size is exact multiple of 64K, dwByteCount will be zero after
		// all the data has been read from the input file. In this case, simply break
		// from the while loop. The check to do this is below
		if (dwByteCount == 0)
			break;

		if (!fResult)
		{
			MyPrintf("ReadFile failed with {0}\n", Marshal.GetLastWin32Error());
			return false;
		}

		finished = (dwByteCount < IN_BUFFER_SIZE);

		// Encrypt/Decrypt depending on the required action.
		if (fEncrypt)
		{
			fResult = CryptEncrypt(hSessionKey, 0, finished, 0, pbBuffer, ref dwByteCount, OUT_BUFFER_SIZE);
			if (!fResult)
			{
				MyPrintf("CryptEncrypt failed with {0:X}\n", Marshal.GetLastWin32Error());
				return false;
			}
		}
		else
		{
			fResult = CryptDecrypt(hSessionKey, 0, finished, 0, pbBuffer, ref dwByteCount);
			if (!fResult)
			{
				MyPrintf("CryptDecrypt failed with {0:X}\n", Marshal.GetLastWin32Error());
				return false;
			}
		}

		// Write the encrypted/decrypted data to the output file.
		fResult = WriteFile(hOutFile, pbBuffer, dwByteCount, out var dwBytesWritten, default);
		if (!fResult)
		{
			MyPrintf("WriteFile failed with {0}\n", Marshal.GetLastWin32Error());
			return false;
		}

	} while (!finished);

	if (fEncrypt)
		MyPrintf("File {0} is encrypted successfully!\n", lpszInputFileName);
	else
		MyPrintf("File {0} is decrypted successfully!\n", lpszInputFileName);

	fSuccess = true;

	return fSuccess;
}