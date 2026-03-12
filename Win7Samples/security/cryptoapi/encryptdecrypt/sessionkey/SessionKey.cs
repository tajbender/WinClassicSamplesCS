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

byte[] pbBuffer = new byte[OUT_BUFFER_SIZE];

if (args.Length != 4)
{
	PrintUsage();
	return;
}

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

// Open the input file to be encrypted or decrypted
using var hInFile = CreateFile(args[2], FileAccess.GENERIC_READ, 0, default, CreationOption.OPEN_EXISTING,
	FileFlagsAndAttributes.FILE_ATTRIBUTE_NORMAL, default);
if (!hInFile)
{
	Console.Write("CreateFile failed with {0}\n", GetLastError());
	return;
}

// Open the output file to write the encrypted or decrypted data
using var hOutFile = CreateFile(args[3], FileAccess.GENERIC_WRITE, 0, default, CreationOption.CREATE_ALWAYS,
	FileFlagsAndAttributes.FILE_ATTRIBUTE_NORMAL, default);
if (!hOutFile)
{
	Console.Write("CreateFile failed with {0}\n", GetLastError());
	return;
}

// Acquire a handle to MS_DEF_PROV using CRYPT_VERIFYCONTEXT for dwFlags
// parameter as we are going to do only session key encryption or decryption
var fResult = CryptAcquireContext(out var hProv, default, CryptProviderName.MS_DEF_PROV, PROV_RSA_FULL, CryptAcquireContextFlags.CRYPT_VERIFYCONTEXT);
if (!fResult)
{
	Console.Write("CryptAcquireContext failed with {0:X}\n", GetLastError());
	return;
}

fResult = CryptCreateHash(hProv, ALG_ID.CALG_SHA1, 0, 0, out var hHash);
if (!fResult)
{
	Console.Write("CryptCreateHash failed with {0:X}\n", GetLastError());
	return;
}

// Hash the supplied secret password
SafeLPTSTR pdh = new(args[0]);
fResult = CryptHashData(hHash, pdh, (uint)args[0].Length, 0);
if (!fResult)
{
	Console.Write("CryptHashData failed with {0:X}\n", GetLastError());
	return;
}

// Derive a symmetric session key from password hash
fResult = CryptDeriveKey(hProv, ALG_ID.CALG_RC4, hHash, 0, out var hSessionKey);
if (!fResult)
{
	Console.Write("CryptDeriveKey failed with {0:X}\n", GetLastError());
	return;
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
		Console.Write("ReadFile failed with {0}\n", GetLastError());
		return;
	}

	finished = (dwByteCount < IN_BUFFER_SIZE);

	// Encrypt/Decrypt depending on the required action.
	if (fEncrypt)
	{
		fResult = CryptEncrypt(hSessionKey, 0, finished, 0, pbBuffer, ref dwByteCount, OUT_BUFFER_SIZE);
		if (!fResult)
		{
			Console.Write("CryptEncrypt failed with {0:X}\n", GetLastError());
			return;
		}
	}
	else
	{
		fResult = CryptDecrypt(hSessionKey, 0, finished, 0, pbBuffer, ref dwByteCount);
		if (!fResult)
		{
			Console.Write("CryptDecrypt failed with {0:X}\n", GetLastError());
			return;
		}
	}

	// Write the encrypted/decrypted data to the output file.
	fResult = WriteFile(hOutFile, pbBuffer, dwByteCount, out var dwBytesWritten, default);
	if (!fResult)
	{
		Console.Write("WriteFile failed with {0}\n", GetLastError());
		return;
	}

} while (!finished);

if (fEncrypt)
	Console.Write("File {0} is encrypted successfully!\n", args[2]);
else
	Console.Write("File {0} is decrypted successfully!\n", args[2]);

void PrintUsage()
{
	Console.Write("SessionKey <password> [</e>|</d>] <InputFile> <OutputFile>\n");
	Console.Write("/e for Encryption\n");
	Console.Write("/d for Decryption\n");
}