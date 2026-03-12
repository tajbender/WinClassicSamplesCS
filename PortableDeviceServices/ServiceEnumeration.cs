using Vanara.PInvoke;
using static Vanara.PInvoke.PortableDeviceApi;

internal partial class Program
{
	public const string CLIENT_NAME = "WPD Services Sample Application";
	public const int CLIENT_MAJOR_VER = 1;
	public const int CLIENT_MINOR_VER = 0;
	public const int CLIENT_REVISION = 0;

	// Creates and populates an IPortableDeviceValues with information about
	// this application. The IPortableDeviceValues is used as a parameter
	// when calling the IPortableDeviceService::Open() method.
	static void GetClientInformation(out IPortableDeviceValues clientInformation)
	{
		// Client information is optional. The client can choose to identify itself, or
		// to remain unknown to the driver. It is beneficial to identify yourself because
		// drivers may be able to optimize their behavior for known clients. (e.g. An
		// IHV may want their bundled driver to perform differently when connected to their
		// bundled software.)
		// CoCreate an IPortableDeviceValues interface to hold the client information.
		clientInformation = new();

		// Attempt to set all bits of client information
		clientInformation.SetStringValue(WPD_CLIENT_NAME, CLIENT_NAME);

		clientInformation.SetUnsignedIntegerValue(WPD_CLIENT_MAJOR_VERSION, CLIENT_MAJOR_VER);

		clientInformation.SetUnsignedIntegerValue(WPD_CLIENT_MINOR_VERSION, CLIENT_MINOR_VER);

		clientInformation.SetUnsignedIntegerValue(WPD_CLIENT_REVISION, CLIENT_REVISION);

		// Some device drivers need to impersonate the caller in order to function correctly. Since our application does not
		// need to restrict its identity, specify SECURITY_IMPERSONATION so that we work with all devices.
		clientInformation.SetUnsignedIntegerValue(WPD_CLIENT_SECURITY_QUALITY_OF_SERVICE, 2U /*SECURITY_IMPERSONATION*/);
	}

	// Reads and displays the device friendly name for the specified PnPDeviceID string
	static void DisplayFriendlyName([In] IPortableDeviceManager deviceManager, string pnpDeviceID)
	{
		// 1) Pass default as the string return string parameter to get the total number
		// of characters to allocate for the string value.
		// 2) Allocate the number of characters needed and retrieve the string value.
		string friendlyName = deviceManager.GetDeviceFriendlyName(pnpDeviceID);

		Console.Write("Friendly Name: {0}\n", friendlyName);
	}

	// Reads and displays the device manufacturer for the specified PnPDeviceID string
	static void DisplayManufacturer([In] IPortableDeviceManager deviceManager, string pnpDeviceID)
	{
		// 1) Pass default as the string return string parameter to get the total number
		// of characters to allocate for the string value.
		// 2) Allocate the number of characters needed and retrieve the string value.
		string manufacturer = deviceManager.GetDeviceManufacturer(pnpDeviceID);
		Console.Write("Manufacturer: {0}\n", manufacturer);
	}

	// Reads and displays the device discription for the specified PnPDeviceID string
	static void DisplayDescription([In] IPortableDeviceManager deviceManager, string pnpDeviceID)
	{
		// 1) Pass default as the string return string parameter to get the total number
		// of characters to allocate for the string value.
		// 2) Allocate the number of characters needed and retrieve the string value.
		string description = deviceManager.GetDeviceDescription(pnpDeviceID);
		Console.Write("Description: {0}\n", description);
	}

	// Reads and displays information about the device
	static void DisplayDeviceInformation([In] IPortableDeviceServiceManager serviceManager, string pnpServiceID)
	{
		// Retrieve the PnP identifier of the device that contains this service
		string pnpDeviceID = serviceManager.GetDeviceForService(pnpServiceID);

		// Retrieve the IPortableDeviceManager interface from IPortableDeviceServiceManager
		IPortableDeviceManager deviceManager = (IPortableDeviceManager)serviceManager;

		DisplayFriendlyName(deviceManager, pnpDeviceID);
		Console.Write(" ");
		DisplayManufacturer(deviceManager, pnpDeviceID);
		Console.Write(" ");
		DisplayDescription(deviceManager, pnpDeviceID);
		Console.WriteLine();
	}

	// Enumerates all Contacts Services, displaying the associated device of each service.
	static IEnumerable<string> EumerateContactsServices()
	{
		// CoCreate the IPortableDeviceManager interface to enumerate
		// portable devices and to get information about them.
		IPortableDeviceManager deviceManager = new();

		// Retrieve the IPortableDeviceServiceManager interface to enumerate device services.
		IPortableDeviceServiceManager serviceManager = (IPortableDeviceServiceManager)deviceManager;

		// 1) Pass default as the string array pointer to get the total number
		// of devices found on the system.
		string[] pnpDeviceIDs = deviceManager.GetDevices();

		if (pnpDeviceIDs.Length > 0)
		{
			// 2) Allocate an array to hold the PnPDeviceID strings returned from
			// the IPortableDeviceManager::GetDevices method
			int retrievedDeviceIDCount = pnpDeviceIDs.Length;

			// For each device found, find the contacts service
			for (uint index = 0; index < retrievedDeviceIDCount; index++)
			{
				// Pass default as the string array pointer to get the total number
				// of contacts services (SERVICE_Contacts) found on the device.
				// To find the total number of all services on the device, use GUID_DEVINTERFACE_WPD_SERVICE.
				uint pnpServiceIDCount = 0;
				serviceManager.GetDeviceServices(pnpDeviceIDs[index], SERVICE_Contacts, null!, ref pnpServiceIDCount);
				if (pnpServiceIDCount > 0)
				{
					// For simplicity, we are only using the first contacts service on each device
					pnpServiceIDCount = 1;
					string[] pnpServiceID = new string[pnpServiceIDCount];
					serviceManager.GetDeviceServices(pnpDeviceIDs[index], SERVICE_Contacts, pnpServiceID, ref pnpServiceIDCount);
					if (!string.IsNullOrEmpty(pnpServiceID[0]))
					{
						// We've found the service, display it and save its PnP Identifier
						yield return pnpServiceID[0];

						Console.Write("[{0}] ", index);

						// Display information about the device that contains this service.
						DisplayDeviceInformation(serviceManager, pnpServiceID[0]);
					}
					else
					{
						Console.Write("! Failed to get the first contacts service from '{0}'\n", pnpDeviceIDs[index]);
					}
				}
			}
		}
	}

	// Calls EnumerateDeviceServices() function to display device services on the system
	// and to obtain the total number of device services found. If 1 or more device services
	// are found, this function prompts the user to choose a device service using
	// a zero-based index.
	static void ChooseDeviceService(out IPortableDeviceService? service)
	{
		service = default;

		// Fill out information about your application, so the device knows
		// who they are speaking to.
		GetClientInformation(out var clientInformation);

		// Enumerate and display all Contacts services
		string[] contactsServices = [..EumerateContactsServices()];

		uint pnpServiceIDCount = (uint)contactsServices.Length;
		if (pnpServiceIDCount > 0)
		{
			// Prompt user to enter an index for the device they want to choose.
			Console.Write("Enter the index of the Contacts device service you wish to use.\n>");
			var selection = Console.ReadLine();
			uint selectedServiceIndex = uint.TryParse(selection, out var u) ? u : 0;
			if (selectedServiceIndex == 0 || selectedServiceIndex >= pnpServiceIDCount)
			{
				Console.Write("An invalid Contacts device service index was specified, defaulting to the first in the list.\n");
			}

			// CoCreate the IPortableDeviceService interface and call Open() with
			// the chosen PnPServiceID string.
			IPortableDeviceService serviceTemp = new();

			try
			{
				serviceTemp.Open(contactsServices[selectedServiceIndex], clientInformation);
			}
			catch (UnauthorizedAccessException)
			{
				Console.Write("Failed to Open the service for Read Write access, will open it for Read-only access instead\n");

				clientInformation.SetUnsignedIntegerValue(WPD_CLIENT_DESIRED_ACCESS, ACCESS_MASK.GENERIC_READ);

				serviceTemp.Open(contactsServices[selectedServiceIndex], clientInformation);
			}
			catch (Exception ex)
			{
				Console.Write("! Failed to Open the service, hr = 0x{0:X}\n", ex.HResult);
			}

			Console.Write("Successfully opened the selected Contacts service\n");
		}

		// If no devices with the Contacts service were found on the system, just exit this function.
	}
}