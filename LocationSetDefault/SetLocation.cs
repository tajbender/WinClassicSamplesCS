using System.Diagnostics;
using Vanara.PInvoke;
using static Vanara.PInvoke.LocationApi;

const string PROMPT_ADDRESS1 = "\nEnter the first line of the street address to set. \nInput that is not less than 80 characters will be truncated\n";
const string PROMPT_ADDRESS2 = "\nEnter the second line of the street address to set.\n";
const string PROMPT_CITY = "\nEnter the city to set.\n";
const string PROMPT_STATEPROVINCE = "\nEnter the state/province to set.\n";
const string PROMPT_POSTALCODE = "\nEnter the postal code to set.\n";
const string PROMPT_COUNTRYREGION = "\nEnter a valid ISO-3166 two-letter country code.\n";
const string PROMPT_LATITUDE = "\nEnter a valid latitude between -90 and 90.\n";
const string PROMPT_LONGITUDE = "\nEnter a valid longitude between -180 and 180.\n";
const string PROMPT_ERRORRADIUS = "\nEnter a non-zero error radius for the latitude/longitude location, in meters.\n";
const string PROMPT_ALTITUDE = "\nEnter a value for altitude.\n";
const string PROMPT_ALTITUDEERROR = "\nEnter a value for the altitude margin of error, in meters.\n";

Console.WriteLine("Location API SDK Default Location Sample");

try
{
	// The location report object
	CLocationReport pNewLocationReport = new();

	// Create the new location report.
	EnterLocation(pNewLocationReport);
	// CoCreate the default location object for getting and setting default location
	IDefaultLocation spDefaultLocation = new();
	// set the civic address fields of the Default Location
	spDefaultLocation.SetReport((ICivicAddressReport)pNewLocationReport);
	// Interface for latitude/longitude reports
	spDefaultLocation.SetReport((ILatLongReport)pNewLocationReport);
}
catch (Exception ex)
{
	Console.WriteLine($"Error: {ex.Message}");
}

// Gets new location data from the user, and prints the report data.
// The location data includes fields for both a civic address report 
// and a latitude/longitude report.
// The error radius, altitude, and altitude error fields are not
// displayed in the Default Location Control Panel, but they are 
// available from the Location API.
void EnterLocation(CLocationReport pLocationReport)
{
	// prompt user to enter line 1 of the street address.
	pLocationReport.Address1 = SafeGetws(PROMPT_ADDRESS1);

	// prompt user to enter line 2 of the street address.
	pLocationReport.Address2 = SafeGetws(PROMPT_ADDRESS2);

	// prompt user to enter the city.
	pLocationReport.City = SafeGetws(PROMPT_CITY);

	// prompt user to enter the state/province.
	pLocationReport.StateProvince = SafeGetws(PROMPT_STATEPROVINCE);

	// prompt user to enter the postal code.
	pLocationReport.PostalCode = SafeGetws(PROMPT_POSTALCODE) ?? "";

	// prompt user to enter the country code.
	pLocationReport.CountryRegion = SafeGetws(PROMPT_COUNTRYREGION) ?? "";

	// prompt the user to enter latitude
	pLocationReport.Latitude = SafeGetDbl(PROMPT_LATITUDE);

	// prompt the user to enter longitude
	pLocationReport.Longitude = SafeGetDbl(PROMPT_LONGITUDE);

	// prompt the user to enter the error radius for the latitude/longitude 
	pLocationReport.ErrorRadius = SafeGetDbl(PROMPT_ERRORRADIUS);

	// prompt the user to enter altitude
	pLocationReport.Altitude = SafeGetDbl(PROMPT_ALTITUDE);

	// prompt the user to enter altitude error
	pLocationReport.AltitudeError = SafeGetDbl(PROMPT_ALTITUDEERROR);

	// Print the Guid 
	PrintGUID(pLocationReport.SensorId);

	// Print the timestamp
	PrintTime(pLocationReport.Timestamp);

	PrintCivicAddress(pLocationReport);
	PrintLatLong(pLocationReport);
}

// Prints the fields of a civic address location report.
// Empty fields are not printed.
void PrintCivicAddress(ICivicAddressReport pCivicAddressReport)
{
	Debug.Assert(pCivicAddressReport is not null);

	Console.Write("\n\n");

	var s = pCivicAddressReport.GetAddressLine1();
	if (!string.IsNullOrEmpty(s))
		Console.Write($"\tAddress Line 1:\t{s}\n");

	s = pCivicAddressReport.GetAddressLine2();
		Console.Write($"\tAddress Line 2:\t{s}\n");

	s = pCivicAddressReport.GetPostalCode();
	if (!string.IsNullOrEmpty(s))
		Console.Write($"\tPostal Code:\t{s}\n");

	s = pCivicAddressReport.GetCity();
	if (!string.IsNullOrEmpty(s))
		Console.Write($"\tCity:\t\t{s}\n");

	s = pCivicAddressReport.GetStateProvince();
	if (!string.IsNullOrEmpty(s))
		Console.Write($"\tState/Province:\t{s}\n");

	s = pCivicAddressReport.GetCountryRegion();
	if (!string.IsNullOrEmpty(s))
		// Country/Region is an ISO-3166 two-letter or three-letter code.
		Console.Write($"\tCountry/Region:\t{s}\n\n");
}

// Prints the latitude and longitude from a latitude/longitude report
void PrintLatLong(ILatLongReport pLatLongReport)
{
	Debug.Assert(pLatLongReport is not null);

	Console.Write("\n\n");

	var d = pLatLongReport.GetLatitude();
	Console.Write($"\tLatitude:\t{d}\n");

	d = pLatLongReport.GetLongitude();
	Console.Write($"\tLongitude:\t{d}\n");

	d = pLatLongReport.GetErrorRadius();
	Console.Write($"\tError Radius:\t{d}\n");

	d = pLatLongReport.GetAltitude();
	Console.Write($"\tAltitude:\t{d}\n");

	d = pLatLongReport.GetAltitudeError();
	Console.Write($"\tAltitude Error:\t{d}\n");
}

// Prints a Guid in a friendly format.
void PrintGUID(in Guid guid) => Console.Write($"\nThe Guid of the location provider is: {guid}");

// Prints the hour, minute, and second from a timestamp.
// The time is Coordinated Universal Time (UTC).
void PrintTime(SYSTEMTIME st) => Console.Write($"\nThe report timestamp is M:{st.wMonth} D:{st.wDay} {st.wHour}:{st.wMinute}:{st.wSecond} UTC\n");

// Helper function that strips the newline character 
// from the line obtained by fgetws. 
string? SafeGetws(string prompt) { Console.Write(prompt); return Console.ReadLine(); }

// Helper function that strips the newline character 
// from the line obtained by fgetws. 
double SafeGetDbl(string prompt) { Console.Write(prompt); var l = Console.ReadLine(); return double.TryParse(l, out var d) ? d : 0; }