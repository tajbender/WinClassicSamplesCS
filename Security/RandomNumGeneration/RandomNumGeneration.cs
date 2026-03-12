using static Vanara.PInvoke.BCrypt;
using Vanara.InteropServices;

const int BufferSize = 128; // Size of the buffer in bytes
SafeHGlobalHandle Buffer = new(BufferSize);

//
// Fill the buffer with random bytes
//

var Status = BCryptGenRandom(default, // Alg Handle pointer; NUll is passed as BCRYPT_USE_SYSTEM_PREFERRED_RNG flag is used
	Buffer, // Address of the buffer that recieves the random number(s)
	BufferSize, // Size of the buffer in bytes
	GenRandomFlags.BCRYPT_USE_SYSTEM_PREFERRED_RNG); // Flags 

if (Status.Failed)
{
	Console.WriteLine($"BCryptGenRandom failed with status: {Status}");
	return (int)Status;
}

return 0;