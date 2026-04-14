using Vanara.PInvoke;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.LocationApi;
using static Vanara.PInvoke.SensorsApi;

// Report object that implements ICivicAddressReport and ILatLongReport
internal class CLocationReport : ICivicAddressReport, ILatLongReport
{
	public CLocationReport()
	{
		// This timestamp is not the same timestamp that is returned in a location report retrieved by using ILocation::GetReport
		GetSystemTime(out SYSTEMTIME systemtime);
		Timestamp = systemtime;
	}

	// Variables for civic address fields
	public string? Address1 { get; set; }

	public string? Address2 { get; set; }
	public double Altitude { get; set; }
	public double AltitudeError { get; set; }
	public string? City { get; set; }
	public string CountryRegion { get; set; } = "";
	public double ErrorRadius { get; set; } = 1000.0;

	// Variables for latitude/longitude report data
	public double Latitude { get; set; }

	public double Longitude { get; set; }
	public string PostalCode { get; set; } = "";

	// The Guid of the default location provider is {682F38CA-5056-4A58-B52E-B516623CF02F} However, the SENSOR_ID is not required to set
	// default location and this value is not the same as the SENSOR_ID in a location report retrieved by using ILocation::GetReport
	public Guid SensorId { get; } = new("682F38CA-5056-4A58-B52E-B516623CF02F");

	public string? StateProvince { get; set; }

	// Variables for other values that are used in a location report.
	public SYSTEMTIME Timestamp { get; private set; }

	[return: MarshalAs(UnmanagedType.BStr)]
	public string GetAddressLine1() => Address1 ?? string.Empty;

	[return: MarshalAs(UnmanagedType.BStr)]
	public string GetAddressLine2() => Address2 ?? string.Empty;

	public double GetAltitude() => Altitude;

	public double GetAltitudeError() => AltitudeError;

	[return: MarshalAs(UnmanagedType.BStr)]
	public string GetCity() => City ?? string.Empty;

	[return: MarshalAs(UnmanagedType.BStr)]
	public string GetCountryRegion() => CountryRegion ?? string.Empty;

	public uint GetDetailLevel() => throw new NotImplementedException();

	public double GetErrorRadius() => ErrorRadius;

	public double GetLatitude() => Latitude;

	public double GetLongitude() => Longitude;

	[return: MarshalAs(UnmanagedType.BStr)]
	public string GetPostalCode() => PostalCode ?? string.Empty;

	public Guid GetSensorID() => SensorId;

	[return: MarshalAs(UnmanagedType.BStr)]
	public string GetStateProvince() => StateProvince ?? string.Empty;

	public SYSTEMTIME GetTimestamp() => Timestamp;

	public Ole32.PROPVARIANT GetValue(in Ole32.PROPERTYKEY pKey) => pKey.pid switch
	{
		var i when i == Sensors.SENSOR_DATA_TYPE_ADDRESS1.pid => new(Address1),
		var i when i == Sensors.SENSOR_DATA_TYPE_ADDRESS2.pid => new(Address2),
		var i when i == Sensors.SENSOR_DATA_TYPE_CITY.pid => new(City),
		var i when i == Sensors.SENSOR_DATA_TYPE_STATE_PROVINCE.pid => new(StateProvince),
		var i when i == Sensors.SENSOR_DATA_TYPE_POSTALCODE.pid => new(PostalCode),
		var i when i == Sensors.SENSOR_DATA_TYPE_COUNTRY_REGION.pid => new(CountryRegion),
		var i when i == Sensors.SENSOR_DATA_TYPE_LATITUDE_DEGREES.pid => new(Latitude),
		var i when i == Sensors.SENSOR_DATA_TYPE_LONGITUDE_DEGREES.pid => new(Longitude),
		var i when i == Sensors.SENSOR_DATA_TYPE_ERROR_RADIUS_METERS.pid => new(ErrorRadius),
		_ => throw new System.ComponentModel.Win32Exception((int)Win32Error.ERROR_NO_DATA)
	};
}