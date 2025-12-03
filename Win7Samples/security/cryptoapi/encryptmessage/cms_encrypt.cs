using System.IO;
using Vanara.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.Crypt32;
using static Vanara.PInvoke.Kernel32;

namespace encryptmessage;

static class cms_encrypt
{
	/*****************************************************************************
	 ReportError
		Prints error information to the console
	*****************************************************************************/
	static void ReportError(string? wszMessage, HRESULT dwErrCode)
	{
		if (!string.IsNullOrEmpty(wszMessage))
			Console.WriteLine(wszMessage);

		var ex = dwErrCode.GetException();
		Console.WriteLine($"Error: 0x{(uint)dwErrCode:X8} ({(uint)dwErrCode})" + ex is null ? "" : $" {ex!.Message}");
	}

	/*****************************************************************************
	  HrLoadFile
	Load file into allocated (*ppbData). 
	The caller must free the memory by LocalFree().
	*****************************************************************************/
	static HRESULT HrLoadFile(string wszFileName, out byte[]? ppbData)
	{
		HRESULT hr = HRESULT.S_OK;

		ppbData = default;

		using var hFile = CreateFile(wszFileName, Kernel32.FileAccess.GENERIC_READ, 0, default, FileMode.Open, 0, default);

		if (hFile.IsInvalid)
		{
			hr = (HRESULT)Win32Error.GetLastError();

			goto CleanUp;
		}

		var pcbData = GetFileSize(hFile, out _);
		if (pcbData == 0)
		{
			hr = HRESULT.S_FALSE;
			goto CleanUp;
		}

		ppbData = new byte[(int)pcbData];

		if (!ReadFile(hFile, ppbData, pcbData, out var cbRead, default))
		{
			hr = (HRESULT)Win32Error.GetLastError();

			goto CleanUp;
		}

		CleanUp:

		if (hr.Failed)
		{
			ppbData = default;
		}

		return hr;
	}

	/*****************************************************************************
	HrSaveFile

	*****************************************************************************/
	static HRESULT HrSaveFile(string wszFileName, byte[] pbData)
	{
		using var hFile = CreateFile(wszFileName, Kernel32.FileAccess.GENERIC_WRITE, 0, default, FileMode.Create, 0, default);

		if (hFile.IsInvalid)
			return (HRESULT)Win32Error.GetLastError();

		if (!WriteFile(hFile, pbData, (uint)pbData.Length, out _))
			return (HRESULT)Win32Error.GetLastError();

		return HRESULT.S_OK;
	}

	/*****************************************************************************
	Usage

	*****************************************************************************/
	static void Usage(string wsName)
	{
		Console.Write("{0} [Options] {{COMMAND}}\n", wsName);
		Console.Write(" Options:\n");
		Console.Write(" -s {{STORENAME}} : store name, (by default MY)\n");
		Console.Write(" -n {{SubjectName}} : Recepient certificate's CN to search for.\n");
		Console.Write(" (by default \"Test\")\n");
		Console.Write(" -a {{CNGAlgName}} : Encryption algorithm, (by default AES128)\n");
		Console.Write(" -k {{KeySize}} : Encryption key size in bits, (by default 128)\n");
		Console.Write(" COMMANDS:\n");
		Console.Write(" ENCRYPT {{inputfile}} {{outputfile}}\n");
		Console.Write(" | Encrypt message\n");
		Console.Write(" DECRYPT {{inputfile}} {{outputfile}}\n");
		Console.Write(" | Decrypt message\n");
	}

	/*****************************************************************************
	 wmain

	*****************************************************************************/
	static int Main(string[] args)
	{
		HRESULT hr = HRESULT.S_OK;

		bool fEncrypt;

		string pwszInputFile;
		string pwszOutputFile;

		SafePCCERT_CONTEXT? pRecipientCert = default;
		SafeHCERTSTORE? hStoreHandle = default;

		string pwszStoreName = "MY"; // by default
		string pwszCName = "Test"; // by default

		byte[]? pbOutput;

		string pwszAlgName = "AES128";
		uint cKeySize = 128;

		int i;

		//
		// options
		//

		for (i = 0; i < args.Length; i++)
		{
			if (string.Compare(args[i], "/?") == 0 ||
			string.Compare(args[i], "-?") == 0)
			{
				Usage("cms_encrypt.exe");
				goto CleanUp;
			}

			if (args[i][0] != '-')
				break;

			if (string.Compare(args[i], "-s") == 0)
			{
				if (i + 1 >= args.Length)
				{
					hr = HRESULT.E_INVALIDARG;

					goto CleanUp;
				}

				pwszStoreName = args[++i];
			}
			else
			if (string.Compare(args[i], "-n") == 0)
			{
				if (i + 1 >= args.Length)
				{
					hr = HRESULT.E_INVALIDARG;

					goto CleanUp;
				}

				pwszCName = args[++i];
			}
			else
			if (string.Compare(args[i], "-a") == 0)
			{
				if (i + 1 >= args.Length)
				{
					hr = HRESULT.E_INVALIDARG;

					goto CleanUp;
				}

				pwszAlgName = args[++i];
			}
			else
			if (string.Compare(args[i], "-k") == 0)
			{
				if (i + 1 >= args.Length)
				{
					hr = HRESULT.E_INVALIDARG;

					goto CleanUp;
				}

				cKeySize = uint.Parse(args[++i]);
			}
		}

		if (0 == string.Compare(args[i], "ENCRYPT"))
		{
			if (i + 2 >= args.Length)
			{
				hr = HRESULT.E_INVALIDARG;
				goto CleanUp;
			}

			fEncrypt = true;
			pwszInputFile = args[++i];
			pwszOutputFile = args[++i];
		}
		else if (0 == string.Compare(args[i], "DECRYPT"))
		{
			if (i + 2 >= args.Length)
			{
				hr = HRESULT.E_INVALIDARG;
				goto CleanUp;
			}

			fEncrypt = false;
			pwszInputFile = args[++i];
			pwszOutputFile = args[++i];
		}
		else
		{
			hr = HRESULT.E_INVALIDARG;
			goto CleanUp;
		}

		if (i != args.Length - 1)
		{
			hr = HRESULT.E_INVALIDARG;
			goto CleanUp;
		}

		//-------------------------------------------------------------------
		// Open the certificate store to be searched.

		hStoreHandle = CertOpenStore(CertStoreProvider.CERT_STORE_PROV_SYSTEM, 0, default,
			CertStoreFlags.CERT_SYSTEM_STORE_CURRENT_USER, pwszStoreName);

		if (hStoreHandle.IsInvalid)
		{
			hr = (HRESULT)Win32Error.GetLastError();
			goto CleanUp;
		}

		//
		// Load file
		// 

		hr = HrLoadFile(pwszInputFile, out var pbInput);
		if (hr.Failed)
		{
			Console.Write("Unable to read file: {0}\n", pwszInputFile);
			goto CleanUp;
		}

		if (fEncrypt)
		{
			//-------------------------------------------------------------------
			// Get a certificate that has the specified Subject Name

			pRecipientCert = CertFindCertificateInStore(hStoreHandle, CertEncodingType.X509_ASN_ENCODING, 0,
				CertFindType.CERT_FIND_SUBJECT_STR, pwszCName, default);
			// function; In all subsequent
			// calls, it is the last pointer
			// returned by the function
			if (pRecipientCert.IsInvalid)
			{
				hr = (HRESULT)Win32Error.GetLastError();
				goto CleanUp;
			}

			//-------------------------------------------------------------------
			// Initialize the CRYPT_ENCRYPT_MESSAGE_PARA structure. 

			var EncryptParams = new CRYPT_ENCRYPT_MESSAGE_PARA
			{
				cbSize = (uint)Marshal.SizeOf(typeof(CRYPT_ENCRYPT_MESSAGE_PARA)),
				dwMsgEncodingType = CertEncodingType.X509_ASN_ENCODING | CertEncodingType.PKCS_7_ASN_ENCODING
			};

			//-------------------------------------------------------------------
			// Find Content Encryption Algorithm OID

			var pOidInfo = CryptFindOIDInfo(CryptOIDInfoFlags.CRYPT_OID_INFO_NAME_KEY, pwszAlgName,
				OIDGroupId.CRYPT_ENCRYPT_ALG_OID_GROUP_ID | (OIDGroupId)(cKeySize << CRYPT_OID_INFO_OID_GROUP_BIT_LEN_SHIFT));
			if (pOidInfo.IsNull)
			{
				hr = HRESULT.CRYPT_E_UNKNOWN_ALGO;
				Console.Write("FAILED: Unknown algorithm: '{0}'.\n", pwszAlgName);
				goto CleanUp;
			}

			EncryptParams.ContentEncryptionAlgorithm.pszObjId = ((CRYPT_OID_INFO)pOidInfo).pszOID;

			//-------------------------------------------------------------------
			// Call CryptEncryptMessage.

			PCCERT_CONTEXT[] ppRecipientCert = [pRecipientCert];
			if (!CryptEncryptMessage(EncryptParams, ppRecipientCert, pbInput!, out pbOutput))
			{
				hr = (HRESULT)Win32Error.GetLastError();
				goto CleanUp;
			}

			Console.Write("Successfully encrypted message using CryptEncryptMessage.\n");
		}
		else
		{
			//-------------------------------------------------------------------
			// Initialize the CRYPT_DECRYPT_MESSAGE_PARA structure.

			using (var pphStoreHandle = SafeHGlobalHandle.CreateFromList(new HCERTSTORE[] { hStoreHandle }))
			{
				var DecryptParams = new CRYPT_DECRYPT_MESSAGE_PARA
				{
					cbSize = (uint)Marshal.SizeOf(typeof(CRYPT_DECRYPT_MESSAGE_PARA)),
					dwMsgAndCertEncodingType = CertEncodingType.X509_ASN_ENCODING | CertEncodingType.PKCS_7_ASN_ENCODING,
					cCertStore = 1,
					rghCertStore = pphStoreHandle
				};

				//-------------------------------------------------------------------
				// Call CryptDecryptMessage to decrypt the data.

				if (!CryptDecryptMessage(DecryptParams, pbInput!, out pbOutput))
				{
					hr = (HRESULT)Win32Error.GetLastError();
					goto CleanUp;
				}
			}

			Console.Write("Successfully decrypted message using CryptDecryptMessage.\n");
		}

		hr = HrSaveFile(pwszOutputFile, pbOutput);
		if (hr.Failed)
		{
			Console.Write("Unable to save file: {0}\n", pwszOutputFile);

			goto CleanUp;
		}

		hr = HRESULT.S_OK;

		//-------------------------------------------------------------------
		// Clean up memory.

		CleanUp:

		pRecipientCert?.Dispose();
		hStoreHandle?.Dispose();

		if (hr.Failed)
		{
			ReportError(default, hr);
		}

		return (int)hr;
	}
}