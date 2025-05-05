using Vanara.Extensions;
using Vanara.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.BCrypt;
using static Vanara.PInvoke.NCrypt;

const uint AesKeyLength = 16;

byte[] GenericParameter = [
	0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a,
	0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09,
];

byte[] Message = [
	0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a,
	0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a,
];

byte[] Secret = [
	0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a,
	0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a,
];

//-----------------------------------------------------------------------------
//
// wmain
//
//-----------------------------------------------------------------------------
const string KdfKeyName = "SampleKDFKey";

try
{
	// Open Microsoft KSP (Key Storage Provider)

	NCryptOpenStorageProvider(out var ProviderHandle, // Pointer to a variable that recieves the provider handle
		KnownStorageProvider.MS_KEY_STORAGE_PROVIDER, // Storage provider identifier(default terminated unicode string); If default, default provider is loaded
		0).ThrowIfFailed(); // Flags

	// Creating a persisted KDF key KDF Algorithm used: SP800-108 Hmac counter mode
	//
	// NCRYPT_OVERWRITE_KEY_FLAG - If a key already exists in the container with the specified name, the existing key will be overwritten.

	NCryptCreatePersistedKey(ProviderHandle, // Handle of the key storage provider
		out var KdfKeyHandle, // Address of the variable that recieves the key handle
		"SP800_108_CTR_HMAC", // NCryptStandardAlgorithmId.NCRYPT_SP800108_CTR_HMAC_ALGORITHM, // Algorithm name (default terminated unicode string)
		KdfKeyName, // Key name (default terminated unicode string)
		0, // Legacy identifier (AT_KEYEXCHANGE, AT_SIGNATURE or 0 )
		CreatePersistedFlags.NCRYPT_OVERWRITE_KEY_FLAG).ThrowIfFailed(); // Flags; If a key already exists in the container with the specified name, the existing key will be overwritten.

	// Set the secret on the KDF key handle

	NCryptSetProperty(KdfKeyHandle, // Handle of the key storage object
		NCrypt.PropertyName.NCRYPT_KDF_SECRET_VALUE, // Property name (default terminated unicode string)
		Secret, // Address of the buffer that contains the property value
		(uint)Secret.Length, // Size of the buffer in bytes
		0).ThrowIfFailed(); // Flags

	// Finalize the key generation process The key is usable here onwards

	NCryptFinalizeKey(KdfKeyHandle, // Handle of the key - that has to be finalized
		0).ThrowIfFailed(); // Flags

	// Construct parameter list

	// Generic parameters: KDF_GENERIC_PARAMETER and KDF_HASH_ALGORITHM are the generic parameters that can be passed for the
	// following KDF algorithms: BCRYPT/NCRYPT_SP800108_CTR_HMAC_ALGORITHM KDF_GENERIC_PARAMETER = KDF_LABEL||0x00||KDF_CONTEXT
	// BCRYPT/NCRYPT_SP80056A_CONCAT_ALGORITHM KDF_GENERIC_PARAMETER = KDF_ALGORITHMID || KDF_PARTYUINFO || KDF_PARTYVINFO {||
	// KDF_SUPPPUBINFO } {|| KDF_SUPPPRIVINFO } BCRYPT/NCRYPT_PBKDF2_ALGORITHM KDF_GENERIC_PARAMETER = KDF_SALT
	// BCRYPT/NCRYPT_CAPI_KDF_ALGORITHM KDF_GENERIC_PARAMETER = Not used
	//
	// Alternatively, KDF specific parameters can be passed as well. For NCRYPT_SP800108_CTR_HMAC_ALGORITHM: KDF_HASH_ALGORITHM,
	// KDF_LABEL and KDF_CONTEXT are required For NCRYPT_SP80056A_CONCAT_ALGORITHM: KDF_HASH_ALGORITHM, KDF_ALGORITHMID,
	// KDF_PARTYUINFO, KDF_PARTYVINFO are required KDF_SUPPPUBINFO, KDF_SUPPPRIVINFO are optional For NCRYPT_PBKDF2_ALGORITHM
	// KDF_HASH_ALGORITHM is required KDF_ITERATION_COUNT, KDF_SALT are optional Iteration count, (if not specified) will default to
	// 10,000 For NCRYPT_CAPI_KDF_ALGORITHM KDF_HASH_ALGORITHM is required

	// Generic parameters are used in the sample KDF_HASH_ALGORITHM
	//
	//
	// KDF_GENERIC_PARAMETER
	NCryptBuffer[] GenericKdfParameters = [
		new(KeyDerivationBufferType.KDF_HASH_ALGORITHM, StandardAlgorithmId.BCRYPT_SHA256_ALGORITHM),
	new(KeyDerivationBufferType.KDF_GENERIC_PARAMETER, GenericParameter)];

	NCryptBufferDesc GenericKdfParamList = new(GenericKdfParameters);

	// Allocate the AesKeyMaterial on the heap

	using var AesKeyMaterial = new SafeHGlobalHandle(AesKeyLength);
	Win32Error.ThrowLastErrorIfInvalidHandle(AesKeyMaterial);

	// Derive an ephemeral key

	NCryptKeyDerivation(KdfKeyHandle, // Handel of the key
		GenericKdfParamList, // Address of the NCryptBufferDesc structure that contains the KDF parameters
		AesKeyMaterial, // Variable that recieves the derived key material
		AesKeyMaterial.Size, // Size of the buffer in bytes
		out _,
		0).ThrowIfFailed(); // Flags

	// Open an algorithm handle

	BCryptOpenAlgorithmProvider(out var AesAlgHandle, // Alg Handle pointer
		StandardAlgorithmId.BCRYPT_AES_ALGORITHM, // Cryptographic Algorithm name (default terminated unicode string)
		default, // Provider name; if default, the default provider is loaded
		0).ThrowIfFailed(); // Flags

	// Generate an AES key from the key bytes

	BCryptGenerateSymmetricKey(AesAlgHandle, // Algorithm provider handle
		out var AesKeyHandle, // A pointer to key handle
		default, // A pointer to the buffer that recieves the key object;default implies memory is allocated and freed by the function
		0, // Size of the buffer in bytes
		AesKeyMaterial, // A pointer to a buffer that contains the key material
		AesKeyMaterial.Size, // Size of the buffer in bytes
		0).ThrowIfFailed(); // Flags

	// Obtain key object size

	var KeyObjectLength = BCryptGetProperty<uint>(AesAlgHandle, // Handle to a CNG object
		BCrypt.PropertyName.BCRYPT_OBJECT_LENGTH); // Property name (default terminated unicode string)

	// Obtain block size

	var BlockLength = BCryptGetProperty<uint>(AesAlgHandle, // Handle to a CNG object
		BCrypt.PropertyName.BCRYPT_BLOCK_LENGTH); // Property name (default terminated unicode string)

	// Allocate the InitVector on the heap

	using var IVBuffer = new SafeHGlobalHandle(BlockLength);
	Win32Error.ThrowLastErrorIfInvalidHandle(IVBuffer);

	// Generate IV randomly

	BCryptGenRandom(default, // Alg Handle pointer; If default, the default provider is chosen
		IVBuffer, // Address of the buffer that recieves the random number(s)
		IVBuffer.Size, // Size of the buffer in bytes
		GenRandomFlags.BCRYPT_USE_SYSTEM_PREFERRED_RNG).ThrowIfFailed(); // Flags

	// Encrypt plain text

	EncryptData(AesKeyHandle,
		KeyObjectLength,
		IVBuffer,
		StringHelper.GetBytes(ChainingMode.BCRYPT_CHAIN_MODE_CBC, true, CharSet.Unicode),
		Message,
		out var EncryptedMessage);

	// Decrypt Cipher text

	DecryptData(AesKeyHandle,
		KeyObjectLength,
		IVBuffer,
		StringHelper.GetBytes(ChainingMode.BCRYPT_CHAIN_MODE_CBC, true, CharSet.Unicode),
		EncryptedMessage,
		out var DecryptedMessage);

	// Optional : Check if the original message and the message obtained after decrypt are the same

	if (0 != DecryptedMessage.CompareTo(Message))
	{
		throw ((HRESULT)HRESULT.NTE_FAIL).GetException()!;
	}

	Console.Write("Success!\n");
	return 0;
}
catch (Exception ex)
{
	ReportError((HRESULT)ex.HResult);
	return ex.HResult;
}

//-----------------------------------------------------------------------------
//
// Decrypt Data
//
//-----------------------------------------------------------------------------
void DecryptData([In] BCRYPT_KEY_HANDLE KeyHandle, uint KeyObjectLength, SafeAllocatedMemoryHandle InitVector, byte[] chainingMode,
	SafeAllocatedMemoryHandle CipherText, out SafeAllocatedMemoryHandle PlainTextPointer)
{
	// Allocate KeyObject on the heap

	using SafeHGlobalHandle KeyObject = new(KeyObjectLength);
	Win32Error.ThrowLastErrorIfInvalidHandle(KeyObject);

	// Generate a duplicate AES key

	BCryptDuplicateKey(KeyHandle, // A pointer to key handle
		out var DecryptKeyHandle, // A pointer to BCRYPT_KEY_HANDLE that recieves a handle to the duplicate key
		KeyObject, // A pointer to the buffer that recieves the duplicate key object
		KeyObjectLength, // Size of the buffer in bytes
		0).ThrowIfFailed(); // Flags

	// Set the chaining mode on the key handle

	BCryptSetProperty(DecryptKeyHandle, // Handle to a CNG object
		BCrypt.PropertyName.BCRYPT_CHAINING_MODE, // Property name(default terminated unicode string)
		chainingMode, // Address of the buffer that contains the new property value
		(uint)chainingMode.Length, // Size of the buffer in bytes
		0).ThrowIfFailed(); // Flags

	// Copy initialization vector into a temporary initialization vector buffer Because after an encrypt/decrypt operation, the IV buffer
	// is overwritten.

	using SafeHGlobalHandle TempInitVector = new(InitVector.Size);
	Win32Error.ThrowLastErrorIfInvalidHandle(TempInitVector);
	InitVector.CopyTo(TempInitVector);

	// Get CipherText's length If the program can compute the length of cipher text(based on algorihtm and chaining mode info.), this
	// call can be avoided.

	BCryptDecrypt(DecryptKeyHandle, // Handle to a key which is used to encrypt
		CipherText, // Address of the buffer that contains the ciphertext
		CipherText.Size, // Size of the buffer in bytes
		IntPtr.Zero, // A pointer to padding info, used with asymmetric and authenticated encryption; else set to default
		TempInitVector, // Address of the buffer that contains the IV.
		TempInitVector.Size, // Size of the IV buffer in bytes
		IntPtr.Zero, // Address of the buffer the recieves the plaintext
		0, // Size of the buffer in bytes
		out var PlainTextLength, // Variable that recieves number of bytes copied to plaintext buffer
		EncryptFlags.BCRYPT_BLOCK_PADDING).ThrowIfFailed(); // Flags; Block padding allows to pad data to the next block size

	using SafeHGlobalHandle PlainText = new(PlainTextLength);
	Win32Error.ThrowLastErrorIfInvalidHandle(PlainText);

	// Decrypt CipherText

	BCryptDecrypt(DecryptKeyHandle, // Handle to a key which is used to encrypt
		CipherText, // Address of the buffer that contains the ciphertext
		CipherText.Size, // Size of the buffer in bytes
		default, // A pointer to padding info, used with asymmetric and authenticated encryption; else set to default
		TempInitVector, // Address of the buffer that contains the IV.
		TempInitVector.Size, // Size of the IV buffer in bytes
		PlainText, // Address of the buffer the recieves the plaintext
		PlainTextLength, // Size of the buffer in bytes
		out _, // Variable that recieves number of bytes copied to plaintext buffer
		EncryptFlags.BCRYPT_BLOCK_PADDING).ThrowIfFailed(); // Flags; Block padding allows to pad data to the next block size

	PlainTextPointer = PlainText;
}

//-----------------------------------------------------------------------------
//
// Encrypt Data
//
//-----------------------------------------------------------------------------
void EncryptData([In] BCRYPT_KEY_HANDLE KeyHandle, uint KeyObjectLength, SafeAllocatedMemoryHandle InitVector, byte[] chainingMode, byte[] PlainText,
	out SafeAllocatedMemoryHandle CipherTextPointer)
{
	CipherTextPointer = SafeHGlobalHandle.Null;

	// Allocate KeyObject on the heap

	using var KeyObject = new SafeHGlobalHandle(KeyObjectLength);
	Win32Error.ThrowLastErrorIfInvalidHandle(KeyObject);

	// Generate a duplicate AES key

	BCryptDuplicateKey(KeyHandle, // A pointer to key handle
		out var EncryptKeyHandle, // A pointer to BCRYPT_KEY_HANDLE that recieves a handle to the duplicate key
		KeyObject, // A pointer to the buffer that recieves the duplicate key object
		KeyObject.Size, // Size of the buffer in bytes
		0).ThrowIfFailed(); // Flags

	// Set the chaining mode on the key handle

	BCryptSetProperty(EncryptKeyHandle, // Handle to a CNG object
		BCrypt.PropertyName.BCRYPT_CHAINING_MODE, // Property name(default terminated unicode string)
		chainingMode, // Address of the buffer that contains the new property value
		(uint)chainingMode.Length, // Size of the buffer in bytes
		0).ThrowIfFailed(); // Flags

	// Copy initialization vector into a temporary initialization vector buffer Because after an encrypt/decrypt operation, the IV buffer
	// is overwritten.

	using SafeHGlobalHandle TempInitVector = new(InitVector.Size);
	Win32Error.ThrowLastErrorIfInvalidHandle(TempInitVector);
	InitVector.CopyTo(TempInitVector);

	// Get CipherText's length If the program can compute the length of cipher text(based on algorihtm and chaining mode info.), this
	// call can be avoided.

	BCryptEncrypt(EncryptKeyHandle, // Handle to a key which is used to encrypt
		PlainText, // Address of the buffer that contains the plaintext
		(uint)PlainText.Length, // Size of the buffer in bytes
		IntPtr.Zero, // A pointer to padding info, used with asymmetric and authenticated encryption; else set to default
		TempInitVector, // Address of the buffer that contains the IV.
		TempInitVector.Size, // Size of the IV buffer in bytes
		IntPtr.Zero, // Address of the buffer the recieves the ciphertext
		0U, // Size of the buffer in bytes
		out var CipherTextLength, // Variable that recieves number of bytes copied to ciphertext buffer
		EncryptFlags.BCRYPT_BLOCK_PADDING).ThrowIfFailed(); // Flags; Block padding allows to pad data to the next block size

	// Allocate Cipher Text on the heap

	using SafeHGlobalHandle CipherText = new(CipherTextLength);
	Win32Error.ThrowLastErrorIfInvalidHandle(CipherText);

	// Peform encyption For block length messages, block padding will add an extra block

	BCryptEncrypt(EncryptKeyHandle, // Handle to a key which is used to encrypt
		PlainText, // Address of the buffer that contains the plaintext
		(uint)PlainText.Length, // Size of the buffer in bytes
		IntPtr.Zero, // A pointer to padding info, used with asymmetric and authenticated encryption; else set to default
		TempInitVector, // Address of the buffer that contains the IV.
		TempInitVector.Size, // Size of the IV buffer in bytes
		CipherText, // Address of the buffer the recieves the ciphertext
		CipherTextLength, // Size of the buffer in bytes
		out _, // Variable that recieves number of bytes copied to ciphertext buffer
		EncryptFlags.BCRYPT_BLOCK_PADDING).ThrowIfFailed(); // Flags; Block padding allows to pad data to the next block size

	CipherTextPointer = CipherText;
}

//----------------------------------------------------------------------------
//
// ReportError
// Prints error information to the console
//
//----------------------------------------------------------------------------
void ReportError([In] IErrorProvider Status) => Console.Write($"Error:\n{Status}\n");