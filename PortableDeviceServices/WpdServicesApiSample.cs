using static Vanara.PInvoke.PortableDeviceApi;

internal partial class Program
{
	private static int Main(string[] args)
	{
		ChooseDeviceService(out IPortableDeviceService? portableDeviceService);

		if (portableDeviceService is null)
		{
			// No device service was selected, so exit immediately.
			Console.WriteLine("No device service was selected or none exist to be selected. Exiting the application.");
			return 1;
		}

		uint selectionIndex = 0;
		while (selectionIndex != 99)
		{
			Console.Write("""		


			WPD Services Sample Application 
			=======================================
			0. Enumerate all Contacts device services
			1. Choose a Contacts service
			2. Enumerate all content on the service
			3. List all formats supported by the service
			4. List all events supported by the service
			5. List all methods supported by the service
			6. List all abstract services implemented by the service
			7. Read properties on a content object
			8. Write properties on a content object
			9. Invoke methods on the service
			10. Invoke methods asynchronously on the service
			99. Exit

			""");
			var selection = Console.ReadLine();
			selectionIndex = uint.TryParse(selection, out var u) ? u : 100;

			switch (selectionIndex)
			{
				case 0:
					EumerateContactsServices();
					break;
				case 1:
					// Release the old IPortableDeviceService interface before
					// obtaining a new one.
					portableDeviceService = default;
					ChooseDeviceService(out portableDeviceService);
					break;
				case 2:
					EnumerateAllContent(portableDeviceService!);
					break;
				case 3:
					ListSupportedFormats(portableDeviceService!);
					break;
				case 4:
					ListSupportedEvents(portableDeviceService!);
					break;
				case 5:
					ListSupportedMethods(portableDeviceService!);
					break;
				case 6:
					ListAbstractServices(portableDeviceService!);
					break;
				case 7:
					ReadContentProperties(portableDeviceService!);
					break;
				case 8:
					WriteContentProperties(portableDeviceService!);
					break;
				case 9:
					InvokeMethods(portableDeviceService!);
					break;
				case 10:
					InvokeMethodsAsync(portableDeviceService!);
					break;
				case 99:
					return 0;
				default:
					Console.Write("! Failed to read menu selection string input.");
					break;
			}
		}

		return 0;
	}
}