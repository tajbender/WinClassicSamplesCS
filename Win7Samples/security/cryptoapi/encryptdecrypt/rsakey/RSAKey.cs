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

if (args.Length != 5)
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

string lpszContainerName = args[1];

/* Check whether to use current user or local machine key container */
CryptAcquireContextFlags dwFlags;
if (string.Compare(args[3], "/u", StringComparison.OrdinalIgnoreCase) == 0)
{
	dwFlags = 0;
}
else if (string.Compare(args[3], "/m", StringComparison.OrdinalIgnoreCase) == 0)
{
	dwFlags = CryptAcquireContextFlags.CRYPT_MACHINE_KEYSET;
}
else
{
	PrintUsage();
	return;
}

string lpszInputFileName = args[3];
string lpszOutputFileName = args[4];

EncryptDecryptFile(lpszContainerName, dwFlags, lpszInputFileName, lpszOutputFileName, fEncrypt);

void PrintUsage()
{
   MyPrintf("RSAKey [</e>|</d>] <KeyContainerName> [</u>|</m>] <InputFile> <OutputFile>\n");
   MyPrintf("/e for Encryption\n");
   MyPrintf("/d for Decryption\n");
   MyPrintf("/u for current user key container\n");
   MyPrintf("/m for local machine key container\n");
}

void MyPrintf(string lpszFormat, params object?[]? args) => Console.Write(lpszFormat, args);

bool EncryptDecryptFile(string lpszContainerName, CryptAcquireContextFlags dwFlags, string lpszInputFileName, string lpszOutputFileName, bool fEncrypt)
{
	SafeHCRYPTKEY hSessionKey;
	byte[] pbBuffer = new byte[OUT_BUFFER_SIZE];
	bool fSuccess = false;

	// Open the input file to be encrypted or decrypted
	using var hInFile = CreateFile(lpszInputFileName, FileAccess.GENERIC_READ, 0, default, CreationOption.OPEN_EXISTING,
		FileFlagsAndAttributes.FILE_ATTRIBUTE_NORMAL, default);
	if (!hInFile)
	{
		MyPrintf("CreateFile failed with {0}\n", GetLastError());
		return false;
	}

	// Open the output file to write the encrypted or decrypted data
	using var hOutFile = CreateFile(lpszOutputFileName, FileAccess.GENERIC_WRITE, 0, default, CreationOption.CREATE_ALWAYS,
		FileFlagsAndAttributes.FILE_ATTRIBUTE_NORMAL, default);
	if (!hOutFile)
	{
		MyPrintf("CreateFile failed with {0}\n", GetLastError());
		return false;
	}

	// Acquire context for RSA key
	var fResult = CryptAcquireContext(out var hProv, lpszContainerName, CryptProviderName.MS_DEF_PROV, PROV_RSA_FULL, dwFlags);
	if (!fResult)
	{
		if (GetLastError() == Win32Error.NTE_BAD_KEYSET)
		{
			// Create a key container if one does not exist.
			fResult = CryptAcquireContext(out hProv, lpszContainerName, CryptProviderName.MS_DEF_PROV, PROV_RSA_FULL, CryptAcquireContextFlags.CRYPT_NEWKEYSET | dwFlags);
			if (!fResult)
			{
				MyPrintf("CryptAcquireContext (2 failed with {0:X}\n", GetLastError());
				return false;
			}
		}
		else
		{
			MyPrintf("CryptAcquireContext (1 failed with {0:X}\n", GetLastError());
			return false;
		}
	}

	// Get the RSA key handle
	fResult = CryptGetUserKey(hProv, CertKeySpec.AT_KEYEXCHANGE, out var hRSAKey);
	if (!fResult)
	{
		if (GetLastError() == Win32Error.NTE_NO_KEY)
		{
			// Create a key if one does not exist.
			fResult = CryptGenKey(hProv, ALG_ID.AT_KEYEXCHANGE, CryptGenKeyFlags.CRYPT_EXPORTABLE, out hRSAKey);
			if (!fResult)
			{
				MyPrintf("CryptGenKey failed with {0:X}\n", GetLastError());
				return false;
			}
		}
		else
		{
			MyPrintf("CryptGetUserKey failed with {0:X}\n", GetLastError());
			return false;
		}
	}

	if (fEncrypt)
	{
		fResult = CryptGenKey(hProv, ALG_ID.CALG_RC4, CryptGenKeyFlags.CRYPT_EXPORTABLE, out hSessionKey);
		if (!fResult)
		{
			MyPrintf("CryptGenKey failed with {0:X}\n", GetLastError());
			return false;
		}

		// The first call to ExportKey with NULL gets the key size.
		uint dwSessionKeyBlob = 0;
		fResult = CryptExportKey(hSessionKey, hRSAKey, BlobType.SIMPLEBLOB, 0, null, ref dwSessionKeyBlob);
		if (!fResult)
		{
			MyPrintf("CryptExportKey failed with {0:X}\n", GetLastError());
			return false;
		}

		// Allocate memory for Encrypted Session key blob
		using SafeLocalHandle pbSessionKeyBlob = new(dwSessionKeyBlob);
		if (!pbSessionKeyBlob)
		{
			MyPrintf("LocalAlloc failed with {0}\n", GetLastError());
			return false;
		}

		fResult = CryptExportKey(hSessionKey, hRSAKey, BlobType.SIMPLEBLOB, 0, pbSessionKeyBlob, ref dwSessionKeyBlob);
		if (!fResult)
		{
			MyPrintf("CryptExportKey failed with {0:X}\n", GetLastError());
			return false;
		}

		// Write the size of key blob, then the key blob itself, to output file.
		using PinnedObject pdwSessionKeyBlob = new(dwSessionKeyBlob);
		fResult = WriteFile(hOutFile, pdwSessionKeyBlob, sizeof(uint), out var dwBytesWritten, default);
		if (!fResult)
		{
			MyPrintf("WriteFile failed with {0}\n", GetLastError());
			return false;
		}
		fResult = WriteFile(hOutFile, pbSessionKeyBlob, dwSessionKeyBlob, out dwBytesWritten, default);
		if (!fResult)
		{
			MyPrintf("WriteFile failed with {0}\n", GetLastError());
			return false;
		}
	}
	else
	{
		// Read in key block size, then key blob itself from input file.
		fResult = ReadFile(hInFile, out uint dwByteCount);
		if (!fResult)
		{
			MyPrintf("ReadFile failed with {0}\n", GetLastError());
			return false;
		}

		fResult = ReadFile(hInFile, pbBuffer, dwByteCount, out var dwBytesRead);
		if (!fResult)
		{
			MyPrintf("ReadFile failed with {0}\n", GetLastError());
			return false;
		}

		// import key blob into "CSP"
		fResult = CryptImportKey(hProv, pbBuffer, (int)dwByteCount, hRSAKey, 0, out hSessionKey);
		if (!fResult)
		{
			MyPrintf("CryptImportKey failed with {0:X}\n", GetLastError());
			return false;
		}
	}

	bool finished = false;
	do
	{
		// Now read data from the input file 64K bytes at a time.
		fResult = ReadFile(hInFile, pbBuffer, IN_BUFFER_SIZE, out var dwByteCount);

		// If the file size is exact multiple of 64K, dwByteCount will be zero after
		// all the data has been read from the input file. In this case, simply break
		// from the while loop. The check to do this is below
		if (dwByteCount == 0)
			break;

		if (!fResult)
		{
			MyPrintf("ReadFile failed with {0}\n", GetLastError());
			return false;
		}

		finished = (dwByteCount < IN_BUFFER_SIZE);

		// Encrypt/Decrypt depending on the required action.
		if (fEncrypt)
		{
			fResult = CryptEncrypt(hSessionKey, 0, finished, 0, pbBuffer, ref dwByteCount, OUT_BUFFER_SIZE);
			if (!fResult)
			{
				MyPrintf("CryptEncrypt failed with {0:X}\n", GetLastError());
				return false;
			}
		}
		else
		{
			fResult = CryptDecrypt(hSessionKey, 0, finished, 0, pbBuffer, ref dwByteCount);
			if (!fResult)
			{
				MyPrintf("CryptDecrypt failed with {0:X}\n", GetLastError());
				return false;
			}
		}

		// Write the encrypted/decrypted data to the output file.
		fResult = WriteFile(hOutFile, pbBuffer, dwByteCount, out var dwBytesWritten);
		if (!fResult)
		{
			MyPrintf("WriteFile failed with {0}\n", GetLastError());
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
