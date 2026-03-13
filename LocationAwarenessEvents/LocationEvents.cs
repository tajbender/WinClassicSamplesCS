using static Vanara.PInvoke.LocationApi;

Console.WriteLine("Location API SDK Asynchronous Sample");

try
{
	Guid[] REPORT_TYPES = { typeof(ILatLongReport).GUID }; // Array of report types of interest. Other ones include typeof(ICivicAddressReport).Guid

	ILocation spLocation = new(); // Create the Location object
	CLocationEvents pLocationEvents = new(); // Create the callback object

	// Request permissions for this user account to receive location data for all the types defined in REPORT_TYPES (which is currently just
	// one report)
	spLocation.RequestPermissions(default, REPORT_TYPES, 1, false).ThrowIfFailed("Warning: Unable to request permissions."); // false means an asynchronous request

	// Tell the Location API that we want to register for reports (which is currently just one report)
	for (uint index = 0; index < REPORT_TYPES.Length; index++)
		spLocation.RegisterForReport(pLocationEvents, REPORT_TYPES[index], 0).ThrowIfFailed();

	// Wait until user presses a key to exit app. During this time the Location API
	// will send reports to our callback interface on another thread.
	Console.Read();

	// Unregister from reports from the Location API
	for (uint index = 0; index < REPORT_TYPES.Length; index++)
		spLocation.UnregisterForReport(REPORT_TYPES[index]);
}
catch (Exception ex)
{
	Console.WriteLine($"Error: {ex.Message}");
}