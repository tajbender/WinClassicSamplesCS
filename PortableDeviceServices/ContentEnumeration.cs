using Vanara.PInvoke;
using static Vanara.PInvoke.PortableDeviceApi;

internal partial class Program
{
	// This number controls how many object identifiers are requested during each call
	// to IEnumPortableDeviceObjectIDs::Next()
	const int NUM_OBJECTS_TO_REQUEST = 10;

	// Recursively called function which enumerates using the specified
	// object identifier as the parent.

	static void RecursiveEnumerate(string objectID, [In] IPortableDeviceContent2 content)
	{
		// Print the object identifier being used as the parent during enumeration.
		Console.WriteLine(objectID);

		try
		{
			// Get an IEnumPortableDeviceObjectIDs interface by calling EnumObjects with the
			// specified parent object identifier.
			IEnumPortableDeviceObjectIDs enumObjectIDs = content.EnumObjects(pszParentObjectID: objectID); // Starting from the passed in object

			// Enum
			foreach (var objectId in enumObjectIDs.Enumerate())
			{
				RecursiveEnumerate(objectId, content);
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("! Exception during enumeration: " + ex.Message);
		}
	}

	// Enumerate all content on the service starting with the
	// "DEVICE" object
	static void EnumerateAllContent([In] IPortableDeviceService service)
	{
		// Get an IPortableDeviceContent2 interface from the IPortableDeviceService interface to
		// access the content-specific methods.
		IPortableDeviceContent2 content = service.Content();

		// Enumerate content starting from the "DEVICE" object.
		Console.WriteLine();
		RecursiveEnumerate(WPD_DEVICE_OBJECT_ID, content);
	}
}