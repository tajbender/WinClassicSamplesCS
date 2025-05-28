using Vanara.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.AdvApi32;
using static Vanara.PInvoke.BCrypt;
using static Vanara.PInvoke.Crypt32;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.NCrypt;
using System.Diagnostics;

const string MS_STRONG_PROV = "Microsoft Strong Cryptographic Provider";
const string MS_ENH_RSA_AES_PROV = "Microsoft Enhanced RSA and AES Cryptographic Provider";
const string BCRYPT_RSAPUBLIC_BLOB       = "RSAPUBLICBLOB";
const string BCRYPT_RSAPRIVATE_BLOB      = "RSAPRIVATEBLOB";
const string LEGACY_RSAPUBLIC_BLOB       = "CAPIPUBLICBLOB";
const string LEGACY_RSAPRIVATE_BLOB = "CAPIPRIVATEBLOB";

using SafeHGlobalHandle Data = new(
[
	0x04, 0x87, 0xec, 0x66, 0xa8, 0xbf, 0x17, 0xa6,
	0xe3, 0x62, 0x6f, 0x1a, 0x55, 0xe2, 0xaf, 0x5e,
	0xbc, 0x54, 0xa4, 0xdc, 0x68, 0x19, 0x3e, 0x94,
]);

//
// EncryptWithCngDecryptWithCapi
//

EncryptWithCngDecryptWithCapi();

//
// EncryptWithCapiDecryptWithCng
//

EncryptWithCapiDecryptWithCng();

//
// SignWithCngVerifyWithCapi
//

SignWithCngVerifyWithCapi();

//
// SignWithCapiVerifyWithCng
//

SignWithCapiVerifyWithCng();

return 0;

//
// Utilities and helper functions
//

//----------------------------------------------------------------------------
//
// ReverseBytes
// Reverses bytes for big-little endian conversion
//
//----------------------------------------------------------------------------
unsafe bool ReverseBytesUnsafe([In, Out] byte* ByteBuffer, int len)
{
	Debug.Assert((len % 2) == 0);

	for (uint count = 0; count < len / 2; count++)
	{
		byte TmpByteBuffer = *(ByteBuffer + count);
		*(ByteBuffer + count) = *(ByteBuffer + len - count - 1);
		*(ByteBuffer + len - count - 1) = TmpByteBuffer;
	}

	return true;
}

bool ReverseBytes(ISafeMemoryHandle buffer) { unsafe { return ReverseBytesUnsafe((byte*)buffer.DangerousGetHandle(), buffer.Size); } }

//----------------------------------------------------------------------------
//
// SignWithCapiVerifyWithCng
// Computes the hash of a message using SHA-256
//
//----------------------------------------------------------------------------
void SignWithCapiVerifyWithCng()
{
	//delete, ignore errors
	CryptAcquireContext(out var CapiProviderHandle, "test", MS_STRONG_PROV, PROV_RSA_FULL, CryptAcquireContextFlags.CRYPT_DELETEKEYSET);

	//acquire handle to a csp container
	Win32Error.ThrowLastErrorIfFalse(CryptAcquireContext(out CapiProviderHandle, "test", MS_STRONG_PROV, PROV_RSA_FULL, CryptAcquireContextFlags.CRYPT_NEWKEYSET));

	//generate a key
	Win32Error.ThrowLastErrorIfFalse(CryptGenKey(CapiProviderHandle, ALG_ID.CALG_RSA_SIGN, 0, out var CapiKeyHandle));

	//create and sign hash with CAPI API
	Win32Error.ThrowLastErrorIfFalse(CryptCreateHash(CapiProviderHandle, ALG_ID.CALG_SHA1, 0, 0, out var CapiHashHandle));

	Win32Error.ThrowLastErrorIfFalse(CryptHashData(CapiHashHandle, Data, Data.Size, 0));

	uint SignatureLength = 0;
	Win32Error.ThrowLastErrorIfFalse(CryptSignHash(CapiHashHandle, CertKeySpec.AT_SIGNATURE, default, 0, default, ref SignatureLength));

	SafeHeapBlock Signature = new(SignatureLength);

	Win32Error.ThrowLastErrorIfFalse(CryptSignHash(CapiHashHandle, CertKeySpec.AT_SIGNATURE, default, 0, Signature, ref SignatureLength));

	uint BlobLength = 0;
	Win32Error.ThrowLastErrorIfFalse(CryptExportKey(CapiKeyHandle, 0, Crypt32.BlobType.PUBLICKEYBLOB, 0, IntPtr.Zero, ref BlobLength));

	SafeHeapBlock Blob = new(BlobLength);
	Win32Error.ThrowLastErrorIfFalse(CryptExportKey(CapiKeyHandle, 0, Crypt32.BlobType.PUBLICKEYBLOB, 0, Blob, ref BlobLength));

	//verify with cng
	BCryptOpenAlgorithmProvider(out var CngAlgHandle, StandardAlgorithmId.BCRYPT_SHA1_ALGORITHM, default, 0).ThrowIfFailed();

	uint HashObjectLength = BCryptGetProperty<uint>(CngAlgHandle, BCrypt.PropertyName.BCRYPT_OBJECT_LENGTH);
	SafeHeapBlock HashObject = new(HashObjectLength);

	uint HashLength = BCryptGetProperty<uint>(CngAlgHandle, BCrypt.PropertyName.BCRYPT_HASH_LENGTH);
	SafeHeapBlock Hash = new(HashLength);

	BCryptCreateHash(CngAlgHandle, out var CngHashHandle, HashObject, HashObjectLength).ThrowIfFailed();

	BCryptHashData(CngHashHandle, Data, Data.Size, 0).ThrowIfFailed();

	//close the hash
	BCryptFinishHash(CngHashHandle, Hash, HashLength, 0).ThrowIfFailed();

	//reverse since CNG is big endian and CAPI is little endian
	ReverseBytes(Signature);

	NCryptOpenStorageProvider(out var CngProviderHandle, KnownStorageProvider.MS_KEY_STORAGE_PROVIDER, 0).ThrowIfFailed();

	NCryptImportKey(CngProviderHandle, default, LEGACY_RSAPUBLIC_BLOB, default, out var CngTmpKeyHandle, Blob, BlobLength, 0).ThrowIfFailed();

	//specify PKCS padding
	SafeHGlobalStruct<BCRYPT_PKCS1_PADDING_INFO> PKCS1PaddingInfo = new BCRYPT_PKCS1_PADDING_INFO()
	{
		pszAlgId = NCryptStandardAlgorithmId.NCRYPT_SHA1_ALGORITHM
	};

	NCryptVerifySignature(CngTmpKeyHandle, PKCS1PaddingInfo, Hash, HashLength, Signature, SignatureLength, NCryptDecryptFlag.NCRYPT_PAD_PKCS1_FLAG).ThrowIfFailed();

	Console.Write("Success!\n");

	//attempt to delete container
	CryptAcquireContext(out CapiProviderHandle, "test", MS_STRONG_PROV, PROV_RSA_FULL, CryptAcquireContextFlags.CRYPT_DELETEKEYSET);
}

//----------------------------------------------------------------------------
//
// SignWithCngVerifyWithCapi
// Signs the hash of a message
// using BCryptSignHash(..) , DSA-1024
//
//----------------------------------------------------------------------------
void SignWithCngVerifyWithCapi()
{
	NCryptOpenStorageProvider(out var CngProviderHandle, KnownStorageProvider.MS_KEY_STORAGE_PROVIDER, 0).ThrowIfFailed();

	NCryptCreatePersistedKey(CngProviderHandle, out var CapiKeyHandle, NCryptStandardAlgorithmId.NCRYPT_RSA_ALGORITHM, "test", 0, 0).ThrowIfFailed();

	try
	{
		NCryptFinalizeKey(CapiKeyHandle, 0).ThrowIfFailed();

		//open alg provider handle
		BCryptOpenAlgorithmProvider(out var CngAlgHandle, NCryptStandardAlgorithmId.NCRYPT_SHA1_ALGORITHM, default, 0).ThrowIfFailed();

		uint HashObjectLength = BCryptGetProperty<uint>(CngAlgHandle, BCrypt.PropertyName.BCRYPT_OBJECT_LENGTH);

		SafeHeapBlock HashObject = new(HashObjectLength);

		//required size of hash?
		uint HashLength = BCryptGetProperty<uint>(CngAlgHandle, BCrypt.PropertyName.BCRYPT_HASH_LENGTH);

		SafeHeapBlock Hash = new(HashLength);

		BCryptCreateHash(CngAlgHandle, out var CapiHashHandle, HashObject, HashObjectLength).ThrowIfFailed();

		BCryptHashData(CapiHashHandle, Data, Data.Size, 0).ThrowIfFailed();

		BCryptFinishHash(CapiHashHandle, Hash, HashLength, 0).ThrowIfFailed();

		SafeHGlobalStruct<BCRYPT_PKCS1_PADDING_INFO> PKCS1PaddingInfo = new BCRYPT_PKCS1_PADDING_INFO() { pszAlgId = NCryptStandardAlgorithmId.NCRYPT_SHA1_ALGORITHM };

		NCryptSignHash(CapiKeyHandle, PKCS1PaddingInfo, Hash, HashLength, default, 0, out var SignatureLength, NCryptDecryptFlag.NCRYPT_PAD_PKCS1_FLAG).ThrowIfFailed();

		SafeHeapBlock Signature = new(SignatureLength);

		NCryptSignHash(CapiKeyHandle, PKCS1PaddingInfo, Hash, HashLength, Signature, SignatureLength, out SignatureLength, NCryptDecryptFlag.NCRYPT_PAD_PKCS1_FLAG).ThrowIfFailed();

		NCryptExportKey(CapiKeyHandle, default, LEGACY_RSAPUBLIC_BLOB, out var Blob).ThrowIfFailed();

		ReverseBytes(Signature);

		//temporarily import the key into a verify context container and decrypt
		Win32Error.ThrowLastErrorIfFalse(CryptAcquireContext(out var CapiLocProvHandle, default, MS_ENH_RSA_AES_PROV, PROV_RSA_AES, CryptAcquireContextFlags.CRYPT_VERIFYCONTEXT));

		Win32Error.ThrowLastErrorIfFalse(CryptImportKey(CapiLocProvHandle, Blob, Blob.Size, 0, 0, out var CngTmpKeyHandle));

		Win32Error.ThrowLastErrorIfFalse(CryptCreateHash(CapiLocProvHandle, ALG_ID.CALG_SHA1, 0, 0, out var HashHandle));

		Win32Error.ThrowLastErrorIfFalse(CryptHashData(HashHandle, Data, Data.Size, 0));

		Win32Error.ThrowLastErrorIfFalse(CryptVerifySignature(HashHandle, Signature, SignatureLength, CngTmpKeyHandle, default, 0));

		Console.Write("Success!\n");
	}
	finally
	{
		NCryptDeleteKey(CapiKeyHandle, 0);
	}
}

//-----------------------------------------------------------------------------------------
//
// EncryptWithCapiDecryptWithCng
// Verifies the signature given the signature Blob, key Blob, and hash of the message
// using BCryptVerifySignature(..) , DSA-1024
// 
//----------------------------------------------------------------------------------------
void EncryptWithCapiDecryptWithCng()
{
	Win32Error.ThrowLastErrorIfFalse(CryptAcquireContext(out var CapiProviderHandle, "test", MS_STRONG_PROV, PROV_RSA_FULL, CryptAcquireContextFlags.CRYPT_NEWKEYSET));

	Win32Error.ThrowLastErrorIfFalse(CryptGenKey(CapiProviderHandle, ALG_ID.CALG_RSA_KEYX, CryptGenKeyFlags.CRYPT_EXPORTABLE, out var CapiKeyHandle));

	uint MsgLength = (uint)Data.Size;

	Win32Error.ThrowLastErrorIfFalse(CryptEncrypt(CapiKeyHandle, 0, true, 0, default, ref MsgLength, Data.Size));

	uint EncryptDecryptDataLength = MsgLength;

	// Check to see if the allocated buffer is long enough
	if (EncryptDecryptDataLength < Data.Size)
	{
		throw ((HRESULT)HRESULT.NTE_FAIL).GetException()!;
	}

	//copy input data to buffer
	SafeHeapBlock EncryptDecryptData = new(Data.GetBytes());
	MsgLength = Data.Size;

	// Call CAPI1 to encrypt
	Win32Error.ThrowLastErrorIfFalse(CryptEncrypt(CapiKeyHandle, 0, true, 0, EncryptDecryptData, ref MsgLength, EncryptDecryptDataLength));

	// Export the key from CAPI1: buffer length probe
	uint BlobLength = 0;
	Win32Error.ThrowLastErrorIfFalse(CryptExportKey(CapiKeyHandle, 0, Crypt32.BlobType.PRIVATEKEYBLOB, 0, IntPtr.Zero, ref BlobLength));

	// Allocate memory to export the key to
	SafeHeapBlock Blob = new(BlobLength);

	// Export the key value to the allocated memory buffer
	Win32Error.ThrowLastErrorIfFalse(CryptExportKey(CapiKeyHandle, 0, Crypt32.BlobType.PRIVATEKEYBLOB, 0, Blob, ref BlobLength));

	// CAPI1 returns the key bytes reversed.
	ReverseBytes(EncryptDecryptData);

	// Now it is time to import the exported CAPI1 key into CNG KSP ...
	// Open Microsoft KSP
	NCryptOpenStorageProvider(out var CngProviderHandle, KnownStorageProvider.MS_KEY_STORAGE_PROVIDER, 0).ThrowIfFailed();

	// ... and create a persistent key in the MS KSP
	NCryptCreatePersistedKey(CngProviderHandle, out var CngTmpKeyHandle, NCryptStandardAlgorithmId.NCRYPT_RSA_ALGORITHM, "cngtmpkey", 0, 0).ThrowIfFailed();

	try
	{
		// Set the property of this key: Legacy RSA private key Blob
		NCryptSetProperty(CngTmpKeyHandle, LEGACY_RSAPRIVATE_BLOB, Blob, BlobLength, 0).ThrowIfFailed();

		// And see that the key object is created.
		NCryptFinalizeKey(CngTmpKeyHandle, 0).ThrowIfFailed();

		// Do in place decryption by providing the same pointers
		// to the input and output buffers (Data, ResultLength) couple.
		NCryptDecrypt(CngTmpKeyHandle, EncryptDecryptData, EncryptDecryptDataLength, default, EncryptDecryptData, EncryptDecryptDataLength, out var ResultLength, NCryptDecryptFlag.NCRYPT_PAD_PKCS1_FLAG).ThrowIfFailed();

		//
		// Optional
		//

		if (0 != EncryptDecryptData.CompareTo(Data))
		{
			throw ((HRESULT)HRESULT.NTE_FAIL).GetException()!;
		}

		Console.Write("Success!\n");
	}
	finally
	{
		NCryptDeleteKey(CngTmpKeyHandle, 0);

		//attempt to delete container
		CryptAcquireContext(out CapiProviderHandle, "test", MS_STRONG_PROV, PROV_RSA_FULL, CryptAcquireContextFlags.CRYPT_DELETEKEYSET);
	}
}

//-----------------------------------------------------------------------------------------
//
// EncryptWithCngDecryptWithCapi
// Verifies the signature given the signature Blob, key Blob, and hash of the message
// using BCryptVerifySignature(..) , DSA-1024
// 
//----------------------------------------------------------------------------------------
void EncryptWithCngDecryptWithCapi()
{
	NCryptOpenStorageProvider(out var CngProviderHandle, KnownStorageProvider.MS_KEY_STORAGE_PROVIDER, 0).ThrowIfFailed();

	NCryptCreatePersistedKey(CngProviderHandle, out var CapiKeyHandle, NCryptStandardAlgorithmId.NCRYPT_RSA_ALGORITHM, "test", 0, 0).ThrowIfFailed();

	try
	{
		NCryptSetProperty(CapiKeyHandle, NCrypt.PropertyName.NCRYPT_EXPORT_POLICY_PROPERTY, ExportPolicy.NCRYPT_ALLOW_PLAINTEXT_EXPORT_FLAG, SetPropFlags.NCRYPT_PERSIST_FLAG).ThrowIfFailed();

		NCryptFinalizeKey(CapiKeyHandle, 0);

		NCryptEncrypt(CapiKeyHandle, Data, Data.Size, default, default, 0, out var OutputLength, NCryptDecryptFlag.NCRYPT_PAD_PKCS1_FLAG).ThrowIfFailed();

		SafeHeapBlock Output = new(OutputLength);

		NCryptEncrypt(CapiKeyHandle, Data, Data.Size, default, Output, OutputLength, out var ResultLength, NCryptDecryptFlag.NCRYPT_PAD_PKCS1_FLAG).ThrowIfFailed();

		NCryptExportKey(CapiKeyHandle, default, LEGACY_RSAPRIVATE_BLOB, default, IntPtr.Zero, 0, out var BlobLength, 0).ThrowIfFailed();

		SafeHeapBlock Blob = new(BlobLength);

		NCryptExportKey(CapiKeyHandle, default, LEGACY_RSAPRIVATE_BLOB, default, Blob, BlobLength, out BlobLength, 0).ThrowIfFailed();

		ReverseBytes(Output);

		//temporarily import the key into a verify context container and decrypt
		Win32Error.ThrowLastErrorIfFalse(CryptAcquireContext(out var CapiLocProvHandle, default, MS_STRONG_PROV, PROV_RSA_FULL, CryptAcquireContextFlags.CRYPT_VERIFYCONTEXT));

		Win32Error.ThrowLastErrorIfFalse(CryptImportKey(CapiLocProvHandle, Blob, BlobLength, 0, 0, out var CngTmpKeyHandle));

		Win32Error.ThrowLastErrorIfFalse(CryptDecrypt(CngTmpKeyHandle, 0, true, 0, Output, ref OutputLength));

		if (0 != Output.CompareTo(Data))
		{
			throw ((HRESULT)HRESULT.NTE_FAIL).GetException()!;
		}

		Console.Write("Success!\n");
	}
	finally
	{
		NCryptDeleteKey(CapiKeyHandle, 0);
	}
}