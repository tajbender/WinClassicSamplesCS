using static Vanara.PInvoke.Ole32;
using static Vanara.PInvoke.SensorsApi;

namespace Vanara.PInvoke;

public static partial class LocationApi
{
	public const int LOCATION_API_VERSION = 1;

	/// <summary>Defines possible status for new reports of a particular report type.</summary>
	/// <remarks>
	/// <para>
	/// These values represent status for new reports. The most recent reports remain available through <c>ILocation::GetReport</c>,
	/// regardless of the reported status. If the status is <b>REPORT_RUNNING</b>, the data in a report is new. Otherwise,
	/// <b>ILocation::GetReport</b> provides cached data if cached data is available.
	/// </para>
	/// <para>Examples</para>
	/// <para>The following code example demonstrates how to retrieve the <b>LOCATION_REPORT_STATUS</b> values from <c>ILocation::GetReportStatus</c>.</para>
	/// <para>
	/// <c>// Get the status of this report type if (SUCCEEDED(spLocation-&gt;GetReportStatus(IID_ILatLongReport, &amp;status))) { bool
	/// fIsNotRunning = true; switch (status) { case REPORT_RUNNING: // If the status for the current report is running, // then do not print
	/// any additional message. // Otherwise, print a message indicating that reports may contain cached data. fIsNotRunning = false; break;
	/// case REPORT_NOT_SUPPORTED: wprintf(L"\nThere is no sensor installed for this report type.\n"); break; case REPORT_ERROR:
	/// wprintf(L"\nReport error.\n"); break; case REPORT_ACCESS_DENIED: wprintf(L"\nAccess denied to reports.\n"); break; case
	/// REPORT_INITIALIZING: wprintf(L"\nReport is initializing.\n"); break; } if (true == fIsNotRunning) { wprintf(L"Location reports
	/// returned from GetReport contain cached data.\n"); } }</c>
	/// </para>
	/// <para>
	/// The following code is a sample implementation of the callback function <c>ILocationEvents::OnStatusChanged</c>. This implementation
	/// prints out messages when there is a change in the status of latitude/longitude reports.
	/// </para>
	/// <para>
	/// <c>// This is called when the status of a report type changes. // The LOCATION_REPORT_STATUS enumeration is defined in LocApi.h in
	/// the SDK STDMETHODIMP CLocationEvents::OnStatusChanged(REFIID reportType, LOCATION_REPORT_STATUS status) { if (IID_ILatLongReport ==
	/// reportType) { switch (status) { case REPORT_NOT_SUPPORTED: wprintf(L"\nNo devices detected.\n"); break; case REPORT_ERROR:
	/// wprintf(L"\nReport error.\n"); break; case REPORT_ACCESS_DENIED: wprintf(L"\nAccess denied to reports.\n"); break; case
	/// REPORT_INITIALIZING: wprintf(L"\nReport is initializing.\n"); break; case REPORT_RUNNING: wprintf(L"\nRunning.\n"); break; } } else
	/// if (IID_ICivicAddressReport == reportType) { } return S_OK; }</c>
	/// </para>
	/// </remarks>
	// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/ne-locationapi-location_report_status typedef enum
	// LOCATION_REPORT_STATUS { REPORT_NOT_SUPPORTED = 0, REPORT_ERROR = 1, REPORT_ACCESS_DENIED = 2, REPORT_INITIALIZING = 3, REPORT_RUNNING
	// = 4 } ;
	[PInvokeData("locationapi.h", MSDNShortId = "NE:locationapi.LOCATION_REPORT_STATUS")]
	public enum LOCATION_REPORT_STATUS
	{
		/// <summary>
		/// <para>Value:</para>
		/// <para>0</para>
		/// <para>The requested report type is not supported by the API. No location providers of the requested type are installed.</para>
		/// </summary>
		REPORT_NOT_SUPPORTED,

		/// <summary>
		/// <para>Value:</para>
		/// <para>1</para>
		/// <para>
		/// There was an error when creating the report, or location providers for the requested type are unable to provide any data.
		/// Location providers might be currently unavailable, or location providers cannot obtain any data. For example, this state may
		/// occur when a GPS sensor is indoors and no satellites are in view.
		/// </para>
		/// </summary>
		REPORT_ERROR,

		/// <summary>
		/// <para>Value:</para>
		/// <para>2</para>
		/// <para>No permissions have been granted to access this report type. Call</para>
		/// <para>ILocation::RequestPermissions</para>
		/// <para>.</para>
		/// </summary>
		REPORT_ACCESS_DENIED,

		/// <summary>
		/// <para>Value:</para>
		/// <para>3</para>
		/// <para>The report is being initialized.</para>
		/// </summary>
		REPORT_INITIALIZING,

		/// <summary>
		/// <para>Value:</para>
		/// <para>4</para>
		/// <para>The report is running. New location data for the requested report type is available.</para>
		/// </summary>
		REPORT_RUNNING,
	}

	/// <summary/>
	[ComImport, Guid("C96039FF-72EC-4617-89BD-84D88BEDC722"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
	public interface _ICivicAddressReportFactoryEvents
	{
	}

	/// <summary/>
	[ComImport, Guid("16EE6CB7-AB3C-424B-849F-269BE551FCBC"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
	public interface _ILatLongReportFactoryEvents
	{
	}

	/// <summary><b>ICivicAddressReport</b> represents a location report that contains information in the form of a street address.</summary>
	/// <remarks>Note that any property value can be <b>NULL</b> if the value is not available.</remarks>
	// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nn-locationapi-icivicaddressreport
	[PInvokeData("locationapi.h", MSDNShortId = "NN:locationapi.ICivicAddressReport")]
	[ComImport, Guid("C0B19F70-4ADF-445d-87F2-CAD8FD711792"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), CoClass(typeof(CivicAddressReport))]
	public interface ICivicAddressReport : ILocationReport
	{
		/// <summary>Retrieves the ID of the sensor that generated the location report.</summary>
		/// <returns>Address of a <b>SENSOR_ID</b> that receives the ID of the sensor that generated the location report.</returns>
		/// <remarks>
		/// <para>A sensor ID is a <b>GUID</b>.</para>
		/// <para>Examples</para>
		/// <para>The following example demonstrates how to call <b>GetSensorID</b>.</para>
		/// <para>
		/// <c>// Print the Sensor ID GUID GUID sensorID = {0}; if (SUCCEEDED(spLatLongReport-&gt;GetSensorID(&amp;sensorID))) {
		/// wprintf(L"SensorID: %s\n", GUIDToString(sensorID, szGUID, ARRAYSIZE(szGUID))); }</c>
		/// </para>
		/// </remarks>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilocationreport-getsensorid HRESULT GetSensorID(
		// [out] SENSOR_ID *pSensorID );
		new Guid GetSensorID();

		/// <summary>Retrieves the date and time when the report was generated.</summary>
		/// <returns>
		/// Address of a <b>SYSTEMTIME</b> that receives the date and time when the report was generated. Time stamps are provided as
		/// Coordinated Universal Time (UTC).
		/// </returns>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilocationreport-gettimestamp HRESULT GetTimestamp(
		// [out] SYSTEMTIME *pCreationTime );
		new SYSTEMTIME GetTimestamp();

		/// <summary>Retrieves a property value from the location report.</summary>
		/// <param name="pKey"><b>REFPROPERTYKEY</b> that specifies the name of the property to retrieve.</param>
		/// <returns>Address of a <b>PROPVARIANT</b> that receives the property value.</returns>
		/// <remarks>
		/// <para>The properties may be platform defined or manufacturer defined.</para>
		/// <para>
		/// Platform-defined <b>PROPERTYKEY</b> values for location sensors are defined in the location sensor data types section of sensors.h.
		/// </para>
		/// <para>
		/// The following is a table of some platform-defined properties that are commonly association with location reports. These property
		/// keys have a <b>fmtid</b> field equal to <b>SENSOR_DATA_TYPE_LOCATION_GUID</b>. Additional properties can be found in sensors.h.
		/// If you are implementing your own location report to pass to <c>SetReport</c>, this table indicates which values must be supplied
		/// in your report object's implementation of <b>GetValue</b>.
		/// </para>
		/// <list type="table">
		/// <listheader>
		/// <description>Property key name and type</description>
		/// <description>Description</description>
		/// </listheader>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_LATITUDE <b>VT_R8</b></description>
		/// <description>Degrees latitude where North is positive.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_LONGITUDE <b>VT_R8</b></description>
		/// <description>Degrees longitude where East is positive.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_ALTITUDE_SEALEVEL_METERS <b>VT_R8</b></description>
		/// <description>Altitude with respect to sea level, in meters.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_ALTITUDE_ELLIPSOID_METERS <b>VT_R8</b></description>
		/// <description>Altitude with respect to the reference ellipsoid, in meters.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_SPEED_KNOTS <b>VT_R8</b></description>
		/// <description>Speed measured in knots.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_TRUE_HEADING_DEGREES <b>VT_R8</b></description>
		/// <description>Heading relative to true north in degrees.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_MAGNETIC_HEADING_DEGREES <b>VT_R8</b></description>
		/// <description>Heading relative to magnetic north in degrees.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_MAGNETIC_VARIATION <b>VT_R8</b></description>
		/// <description>Magnetic variation. East is positive.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_ERROR_RADIUS_METERS <b>VT_R8</b></description>
		/// <description>Error radius that indicates accuracy of latitude and longitude in meters.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_ADDRESS1 <b>VT_LPWSTR</b></description>
		/// <description>First line of the address in a civic address report.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_ADDRESS2 <b>VT_LPWSTR</b></description>
		/// <description>Second line of the address in a civic address report.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_CITY <b>VT_LPWSTR</b></description>
		/// <description>City field in a civic address report.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_STATE_PROVINCE <b>VT_LPWSTR</b></description>
		/// <description>State/province field in a civic address report.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_POSTALCODE <b>VT_LPWSTR</b></description>
		/// <description>Postal code field in a civic address report.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_COUNTRY_REGION <b>VT_LPWSTR</b></description>
		/// <description>
		/// Country/region code in a civic address report. The value must be a two-letter or three-letter ISO 3166 country code.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_ALTITUDE_ELLIPSOID_ERROR_METERS <b>VT_R8</b></description>
		/// <description>Altitude error with respect to the reference ellipsoid, in meters.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_ALTITUDE_SEALEVEL_ERROR_METERS <b>VT_R8</b></description>
		/// <description>Altitude error with respect to sea level, in meters.</description>
		/// </item>
		/// </list>
		/// <para></para>
		/// <para>
		/// The following is a table of other platform-defined properties that may occur in location reports but are not specific to
		/// location. The <b>fmtid</b> field of these property keys is <b>SENSOR_PROPERTY_COMMON_GUID</b>.
		/// </para>
		/// <list type="table">
		/// <listheader>
		/// <description>Property key name and type</description>
		/// <description>Description</description>
		/// </listheader>
		/// <item>
		/// <description>SENSOR_PROPERTY_ACCURACY <b>VT_UNKNOWN</b></description>
		/// <description>
		/// IPortableDeviceValues object that contains sensor data type names and their associated accuracies. Accuracy values represent
		/// possible variation from true values. Accuracy values are expressed by using the same units as the data field, except when
		/// otherwise documented.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_CHANGE_SENSITIVITY <b>VT_UNKNOWN</b></description>
		/// <description>
		/// <b>IPortableDeviceValues</b> object that contains sensor data type names and their associated change sensitivity values. Change
		/// sensitivity values represent the amount by which the data field must change before the SENSOR_EVENT_DATA_UPDATED event is raised.
		/// Sensitivity values are expressed by using the same units as the data field, except where otherwise documented. For example, a
		/// change sensitivity value of 2 for SENSOR_DATA_TYPE_TEMPERATURE_CELSIUS would represent a sensitivity of plus or minus 2 degrees Celsius.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_CURRENT_CONNECTION_TYPE <b>VT_UI4</b></description>
		/// <description><c>SensorConnectionType</c> value that contains the current connection type.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_CURRENT_REPORT_INTERVAL <b>VT_UI4</b></description>
		/// <description>
		/// The current elapsed time for sensor data report generation, in milliseconds. Setting a value of zero signals the driver to use
		/// its default report interval. After receiving a value of zero for this property, a driver must return its default report interval,
		/// not zero, when queried. Applications can set this value to request a particular report interval, but multiple applications might
		/// be using the same driver. Therefore, drivers determine the true report interval that is based on internal logic. For example, the
		/// driver might always use the shortest report interval that is requested by the caller.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_DESCRIPTION <b>VT_LPWSTR</b></description>
		/// <description>The sensor description string.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_DEVICE_PATH <b>VT_LPWSTR</b></description>
		/// <description>
		/// Uniquely identifies the device instance with which the sensor is associated. You can use this property to determine whether a
		/// device contains multiple sensors. Device drivers do not have to support this property because the platform provides this value to
		/// applications without querying drivers.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_FRIENDLY_NAME <b>VT_LPWSTR</b></description>
		/// <description>The friendly name for the device.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_LOCATION_DESIRED_ACCURACY <b>VT_UI4</b></description>
		/// <description>
		/// An enumerated value that indicates the type of accuracy handling requested by a client application.
		/// <b>LOCATION_DESIRED_ACCURACY_DEFAULT</b> (0) indicates that the sensor should use the accuracy for which it can optimize power
		/// use and other cost considerations. <b>LOCATION_DESIRED_ACCURACY_HIGH</b> (1) indicates that the sensor should deliver the most
		/// accurate report possible. This includes using services that might charge money, or consuming higher levels of battery power or
		/// connection bandwidth.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_MANUFACTURER <b>VT_LPWSTR</b></description>
		/// <description>The manufacturer's name.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_MIN_REPORT_INTERVAL <b>VT_UI4</b></description>
		/// <description>The minimum elapsed time setting that the hardware supports for sensor data report generation, in milliseconds.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_MODEL <b>VT_LPWSTR</b></description>
		/// <description>The sensor model name.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_PERSISTENT_UNIQUE_ID <b>VT_CLSID</b></description>
		/// <description>
		/// A <b>GUID</b> that identifies the sensor. This value must be unique for each sensor on a device, or across devices of the same
		/// model as enumerated on the computer.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_RANGE_MAXIMUM <b>VT_UKNOWN</b></description>
		/// <description><b>IPortableDeviceValues</b> object that contains sensor data field names and their associated maximum values.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_RANGE_MINIMUM <b>VT_UKNOWN</b></description>
		/// <description><b>IPortableDeviceValues</b> object that contains sensor data field names and their associated minimum values.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_RESOLUTION <b>VT_UKNOWN</b></description>
		/// <description>
		/// <b>IPortableDeviceValues</b> object that contains sensor data field names and their associated resolutions. Resolution values
		/// represent sensitivity to change in the data field. Resolution values are expressed by using the same units as the data field,
		/// except when otherwise documented.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_SERIAL_NUMBER <b>VT_LPWSTR</b></description>
		/// <description>The sensor serial number.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_STATE <b>VT_UI4</b></description>
		/// <description><c>SensorState</c> value that contains the current sensor state.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_TYPE <b>VT_CLSID</b></description>
		/// <description>A <b>GUID</b> that identifies the sensor type. Platform-defined sensor types are defined in Sensors.h.</description>
		/// </item>
		/// </list>
		/// <para></para>
		/// <para>Examples</para>
		/// <para>
		/// The following example demonstrates how to call <b>GetValue</b> to get a property value. You must include sensors.h to use the
		/// constant in the example.
		/// </para>
		/// <para><c>PROPVARIANT pv; HRESULT hr = spLatLongReport-&gt;GetValue(SENSOR_DATA_TYPE_LATITUDE_DEGREES, &amp;pv);</c></para>
		/// <para>
		/// The following example shows how to implement <b>GetValue</b> in your own report object. This implementation allows the caller to
		/// get values for several location report fields. This code requires you to include sensors.h and provarutil.h.
		/// </para>
		/// <para>
		/// <c>STDMETHODIMP CLocationReport::GetValue(REFPROPERTYKEY pKey, PROPVARIANT *pValue) { HRESULT hr = S_OK; if (pKey.fmtid ==
		/// SENSOR_DATA_TYPE_LOCATION_GUID) { // properties for civic address reports if (pKey.pid == SENSOR_DATA_TYPE_ADDRESS1.pid) { hr =
		/// InitPropVariantFromString(m_address1, pValue); } else if (pKey.pid == SENSOR_DATA_TYPE_ADDRESS2.pid) { hr =
		/// InitPropVariantFromString(m_address2, pValue); } else if (pKey.pid == SENSOR_DATA_TYPE_CITY.pid) { hr =
		/// InitPropVariantFromString(m_city, pValue); } else if (pKey.pid == SENSOR_DATA_TYPE_STATE_PROVINCE.pid) { hr =
		/// InitPropVariantFromString(m_stateprovince, pValue); } else if (pKey.pid == SENSOR_DATA_TYPE_POSTALCODE.pid) { hr =
		/// InitPropVariantFromString(m_postalcode, pValue); } else if (pKey.pid == SENSOR_DATA_TYPE_COUNTRY_REGION.pid) { hr =
		/// InitPropVariantFromString(m_countryregion, pValue); } // properties for latitude/longitude reports else if (pKey.pid ==
		/// SENSOR_DATA_TYPE_LATITUDE_DEGREES.pid) { hr = InitPropVariantFromDouble(m_latitude, pValue); } else if (pKey.pid ==
		/// SENSOR_DATA_TYPE_LONGITUDE_DEGREES.pid) { hr = InitPropVariantFromDouble(m_longitude, pValue); } else if (pKey.pid ==
		/// SENSOR_DATA_TYPE_ERROR_RADIUS_METERS.pid) { hr = InitPropVariantFromDouble(m_errorradius, pValue); } else { hr =
		/// HRESULT_FROM_WIN32(ERROR_NO_DATA); PropVariantInit(pValue); } } return hr; }</c>
		/// </para>
		/// </remarks>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilocationreport-getvalue HRESULT GetValue( [in]
		// REFPROPERTYKEY pKey, [out] PROPVARIANT *pValue );
		new PROPVARIANT GetValue(in PROPERTYKEY pKey);

		/// <summary>Retrieves the first line of a street address.</summary>
		/// <returns>Address of a <b>BSTR</b> that receives the first line of a street address.</returns>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-icivicaddressreport-getaddressline1 HRESULT
		// GetAddressLine1( [out] BSTR *pbstrAddress1 );
		[return: MarshalAs(UnmanagedType.BStr)]
		string GetAddressLine1();

		/// <summary>Retrieves the second line of a street address.</summary>
		/// <returns>Address of a <b>BSTR</b> that receives the second line of a street address.</returns>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-icivicaddressreport-getaddressline2 HRESULT
		// GetAddressLine2( [out] BSTR *pbstrAddress2 );
		[return: MarshalAs(UnmanagedType.BStr)]
		string GetAddressLine2();

		/// <summary>Retrieves the city name.</summary>
		/// <returns>Address of a <b>BSTR</b> that receives the city name.</returns>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-icivicaddressreport-getcity HRESULT GetCity( [out]
		// BSTR *pbstrCity );
		[return: MarshalAs(UnmanagedType.BStr)]
		string GetCity();

		/// <summary>Retrieves the state or province name.</summary>
		/// <returns>Address of a <b>BSTR</b> that receives the state or province name.</returns>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-icivicaddressreport-getstateprovince?view=vs-2017
		// HRESULT GetStateProvince( [out] BSTR *pbstrStateProvince );
		[return: MarshalAs(UnmanagedType.BStr)]
		string GetStateProvince();

		/// <summary>Retrieves the postal code.</summary>
		/// <returns>Address of a <b>BSTR</b> that receives the postal code.</returns>
		// https://learn.microsoft.com/en-us/WINDOWS/WIN32/API/locationapi/nf-locationapi-icivicaddressreport-getpostalcode HRESULT
		// GetPostalCode( [out] BSTR *pbstrPostalCode );
		[return: MarshalAs(UnmanagedType.BStr)]
		string GetPostalCode();

		/// <summary>Retrieves the two-letter country or region code.</summary>
		/// <returns>Address of a <b>BSTR</b> that receives the country or region code.</returns>
		/// <remarks>
		/// <para>The two-letter country or region code is in ISO 3166 format.</para>
		/// <para>Examples</para>
		/// <para>The following example demonstrates how to call <b>GetCountryRegion</b>.</para>
		/// <para>
		/// <c>hr = pCivicAddressReport-&gt;GetCountryRegion(&amp;bstrCountryRegion); if (SUCCEEDED(hr)) { // Country/Region is an ISO-3166-1
		/// two-letter code. wprintf(L"\tCountry/Region:\t%s\n\n", bstrCountryRegion); }</c>
		/// </para>
		/// </remarks>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-icivicaddressreport-getcountryregion HRESULT
		// GetCountryRegion( [out] BSTR *pbstrCountryRegion );
		[return: MarshalAs(UnmanagedType.BStr)]
		string GetCountryRegion();

		/// <summary>Reserved.</summary>
		/// <param name="pDetailLevel">Reserved.</param>
		/// <returns>If this method succeeds, it returns <b>S_OK</b>. Otherwise, it returns an <b>HRESULT</b> error code.</returns>
		/// <remarks>
		/// To determine whether a civic address report contains valid data for a particular field, simply inspect the field's contents. If
		/// the field contains a value, you can assume that the field contains the most accurate information available.
		/// </remarks>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-icivicaddressreport-getdetaillevel HRESULT
		// GetDetailLevel( [out] DWORD *pDetailLevel );
		uint GetDetailLevel();
	}

	/// <summary/>
	[ComImport, Guid("BF773B93-C64F-4bee-BEB2-67C0B8DF66E0"), InterfaceType(ComInterfaceType.InterfaceIsDual), CoClass(typeof(CivicAddressReportFactory))]
	public interface ICivicAddressReportFactory : ILocationReportFactory
	{
		/// <summary/>
		[PreserveSig]
		new HRESULT ListenForReports(uint requestedReportInterval = 0);

		/// <summary/>
		[PreserveSig]
		new HRESULT StopListeningForReports();

		/// <summary/>
		new uint Status { get; }

		/// <summary/>
		new uint ReportInterval { get; set; }

		/// <summary/>
		new uint DesiredAccuracy { get; set; }

		/// <summary/>
		[PreserveSig]
		new HRESULT RequestPermissions(in uint hWnd);

		/// <summary/>
		IDispCivicAddressReport? CivicAddressReport { get; }
	}

	/// <summary><b>IDefaultLocation</b> provides methods used to specify or retrieve the default location.</summary>
	/// <remarks>
	/// <para>
	/// <b>Note</b>  An application does not receive the expected location change event from <c>OnLocationChanged</c> if both of the
	/// following conditions are true. First, the application runs as a service, in the context of the LOCALSERVICE, SYSTEM, or
	/// NETWORKSERVICE user account. Second, the location change event results from changing the default location, either manually when the
	/// user selects <b>Default Location</b> in Control Panel, or programmatically when an application calls <b>IDefaultLocation::SetReport</b>.
	/// </para>
	/// <para></para>
	/// </remarks>
	// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nn-locationapi-idefaultlocation
	[PInvokeData("locationapi.h", MSDNShortId = "NN:locationapi.IDefaultLocation")]
	[ComImport, Guid("A65AF77E-969A-4a2e-8ACA-33BB7CBB1235"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), CoClass(typeof(DefaultLocation))]
	public interface IDefaultLocation
	{
		/// <summary>Sets the default location.</summary>
		/// <param name="reportType"><b>REFIID</b> that represents the interface ID of the type of report that is passed using <i>pLocationReport</i>.</param>
		/// <param name="pLocationReport">
		/// Pointer to the <c>ILocationReport</c> instance that contains the location report from the default location provider.
		/// </param>
		/// <remarks>
		/// <para>
		/// <c>ILocationReport</c> is the base interface of specific location report types. The actual interface you use for
		/// <i>pLocationReport</i> must match the type you specify through <i>reportType</i>.
		/// </para>
		/// <para>Note that the type specified by <i>reportType</i> must be the <b>IID</b> of either <c>ICivicAddressReport</c> or <c>ILatLongReport</c>.</para>
		/// <para>
		/// The latitude and longitude provided in a latitude/longitude report must correspond to a location on the globe. Otherwise this
		/// method returns an <b>HRESULT</b> error value.
		/// </para>
		/// <para>
		/// <b>Note</b>  An application does not receive the expected location change event from <c>OnLocationChanged</c> if both of the
		/// following conditions are true. First, the application runs as a service, in the context of the LOCALSERVICE, SYSTEM, or
		/// NETWORKSERVICE user account. Second, the location change event results from changing the default location, either manually when
		/// the user selects <b>Default Location</b> in Control Panel, or programmatically when an application calls <c>IDefaultLocation::SetReport</c>.
		/// </para>
		/// <para></para>
		/// <para>Examples</para>
		/// <para>The following example shows how to set the default location using a civic address report.</para>
		/// <para>
		/// <c>// set the civic address fields of the Default Location hr = spDefaultLocation-&gt;SetReport(IID_ICivicAddressReport,
		/// spCivicAddressReport); if (E_INVALIDARG == hr) { wprintf(L"The civic address report has invalid data. ");
		/// wprintf(L"Country/region must be a valid ISO-3166 2-letter or 3-letter code.\n"); } else if (E_ACCESSDENIED == hr) {
		/// wprintf(L"Administrator privilege required.\n"); }</c>
		/// </para>
		/// </remarks>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-idefaultlocation-setreport HRESULT SetReport( [in]
		// REFIID reportType, [in] ILocationReport *pLocationReport );
		void SetReport(in Guid reportType, [In, Optional, MarshalAs(UnmanagedType.Interface, IidParameterIndex = 0)] ILocationReport? pLocationReport);

		/// <summary>Retrieves the specified report type from the default location provider.</summary>
		/// <param name="reportType"><b>REFIID</b> representing the interface ID for the type of report being retrieved.</param>
		/// <returns>
		/// The address of a pointer to <c>ILocationReport</c> that receives the specified location report from the default location provider.
		/// </returns>
		/// <remarks>
		/// <para>
		/// <c>ILocationReport</c> is the base interface for specific location report types. The actual interface you use for
		/// <i>ppLocationReport</i> must match the type you specified through <i>reportType</i>.
		/// </para>
		/// <para>
		/// A call to <b>IDefaultLocation::GetReport</b> may result in a notification being displayed in the taskbar, and a Location Activity
		/// event being logged in Event Viewer, if it is the application's first use of location.
		/// </para>
		/// </remarks>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-idefaultlocation-getreport HRESULT GetReport( [in]
		// REFIID reportType, [out] ILocationReport **ppLocationReport );
		ILocationReport? GetReport(in Guid reportType);
	}

	/// <summary>Sets the default location.</summary>
	/// <param name="reportType"><b>REFIID</b> that represents the interface ID of the type of report that is passed using <i>pLocationReport</i>.</param>
	/// <param name="pLocationReport">
	/// Pointer to the <c>ILocationReport</c> instance that contains the location report from the default location provider.
	/// </param>
	/// <remarks>
	/// <para>
	/// <c>ILocationReport</c> is the base interface of specific location report types. The actual interface you use for
	/// <i>pLocationReport</i> must match the type you specify through <i>reportType</i>.
	/// </para>
	/// <para>Note that the type specified by <i>reportType</i> must be the <b>IID</b> of either <c>ICivicAddressReport</c> or <c>ILatLongReport</c>.</para>
	/// <para>
	/// The latitude and longitude provided in a latitude/longitude report must correspond to a location on the globe. Otherwise this
	/// method returns an <b>HRESULT</b> error value.
	/// </para>
	/// <para>
	/// <b>Note</b>  An application does not receive the expected location change event from <c>OnLocationChanged</c> if both of the
	/// following conditions are true. First, the application runs as a service, in the context of the LOCALSERVICE, SYSTEM, or
	/// NETWORKSERVICE user account. Second, the location change event results from changing the default location, either manually when
	/// the user selects <b>Default Location</b> in Control Panel, or programmatically when an application calls <c>IDefaultLocation::SetReport</c>.
	/// </para>
	/// <para></para>
	/// <para>Examples</para>
	/// <para>The following example shows how to set the default location using a civic address report.</para>
	/// <para>
	/// <c>// set the civic address fields of the Default Location hr = spDefaultLocation-&gt;SetReport(IID_ICivicAddressReport,
	/// spCivicAddressReport); if (E_INVALIDARG == hr) { wprintf(L"The civic address report has invalid data. ");
	/// wprintf(L"Country/region must be a valid ISO-3166 2-letter or 3-letter code.\n"); } else if (E_ACCESSDENIED == hr) {
	/// wprintf(L"Administrator privilege required.\n"); }</c>
	/// </para>
	/// </remarks>
	// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-idefaultlocation-setreport HRESULT SetReport( [in]
	// REFIID reportType, [in] ILocationReport *pLocationReport );
	public static void SetReport<T>(this IDefaultLocation @this, [In, Optional] T? pLocationReport) where T : class, ILocationReport =>
		@this.SetReport(typeof(T).GUID, pLocationReport);

	/// <summary>Retrieves the specified report type from the default location provider.</summary>
	/// <param name="reportType"><b>REFIID</b> representing the interface ID for the type of report being retrieved.</param>
	/// <returns>
	/// The address of a pointer to <c>ILocationReport</c> that receives the specified location report from the default location provider.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <c>ILocationReport</c> is the base interface for specific location report types. The actual interface you use for
	/// <i>ppLocationReport</i> must match the type you specified through <i>reportType</i>.
	/// </para>
	/// <para>
	/// A call to <b>IDefaultLocation::GetReport</b> may result in a notification being displayed in the taskbar, and a Location Activity
	/// event being logged in Event Viewer, if it is the application's first use of location.
	/// </para>
	/// </remarks>
	// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-idefaultlocation-getreport HRESULT GetReport( [in]
	// REFIID reportType, [out] ILocationReport **ppLocationReport );
	public static T? GetReport<T>(this IDefaultLocation @this) where T : class, ILocationReport =>
		@this.GetReport(typeof(T).GUID) as T;

	/// <summary/>
	[ComImport, Guid("16FF1A34-9E30-42c3-B44D-E22513B5767A"), InterfaceType(ComInterfaceType.InterfaceIsDual), CoClass(typeof(DispCivicAddressReport))]
	public interface IDispCivicAddressReport
	{
		/// <summary/>
		string AddressLine1 { [return: MarshalAs(UnmanagedType.BStr)] get; }

		/// <summary/>
		string AddressLine2 { [return: MarshalAs(UnmanagedType.BStr)] get; }

		/// <summary/>
		string City { [return: MarshalAs(UnmanagedType.BStr)] get; }

		/// <summary/>
		string StateProvince { [return: MarshalAs(UnmanagedType.BStr)] get; }

		/// <summary/>
		string PostalCode { [return: MarshalAs(UnmanagedType.BStr)] get; }

		/// <summary/>
		string CountryRegion { [return: MarshalAs(UnmanagedType.BStr)] get; }

		/// <summary/>
		uint DetailLevel { get; }

		/// <summary/>
		DATE Timestamp { get; }
	}

	/// <summary/>
	[ComImport, Guid("8AE32723-389B-4A11-9957-5BDD48FC9617"), InterfaceType(ComInterfaceType.InterfaceIsDual), CoClass(typeof(DispLatLongReport))]
	public interface IDispLatLongReport
	{
		/// <summary/>
		double Latitude { get; }

		/// <summary/>
		double Longitude { get; }

		/// <summary/>
		double ErrorRadius { get; }

		/// <summary/>
		double Altitude { get; }

		/// <summary/>
		double AltitudeError { get; }

		/// <summary/>
		DATE Timestamp { get; }
	}

	/// <summary><b>ILatLongReport</b> represents a location report that contains information in the form of latitude and longitude.</summary>
	// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nn-locationapi-ilatlongreport
	[PInvokeData("locationapi.h", MSDNShortId = "NN:locationapi.ILatLongReport")]
	[ComImport, Guid("7FED806D-0EF8-4f07-80AC-36A0BEAE3134"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), CoClass(typeof(LatLongReport))]
	public interface ILatLongReport : ILocationReport
	{
		/// <summary>Retrieves the ID of the sensor that generated the location report.</summary>
		/// <returns>Address of a <b>SENSOR_ID</b> that receives the ID of the sensor that generated the location report.</returns>
		/// <remarks>
		/// <para>A sensor ID is a <b>GUID</b>.</para>
		/// <para>Examples</para>
		/// <para>The following example demonstrates how to call <b>GetSensorID</b>.</para>
		/// <para>
		/// <c>// Print the Sensor ID GUID GUID sensorID = {0}; if (SUCCEEDED(spLatLongReport-&gt;GetSensorID(&amp;sensorID))) {
		/// wprintf(L"SensorID: %s\n", GUIDToString(sensorID, szGUID, ARRAYSIZE(szGUID))); }</c>
		/// </para>
		/// </remarks>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilocationreport-getsensorid HRESULT GetSensorID(
		// [out] SENSOR_ID *pSensorID );
		new Guid GetSensorID();

		/// <summary>Retrieves the date and time when the report was generated.</summary>
		/// <returns>
		/// Address of a <b>SYSTEMTIME</b> that receives the date and time when the report was generated. Time stamps are provided as
		/// Coordinated Universal Time (UTC).
		/// </returns>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilocationreport-gettimestamp HRESULT GetTimestamp(
		// [out] SYSTEMTIME *pCreationTime );
		new SYSTEMTIME GetTimestamp();

		/// <summary>Retrieves a property value from the location report.</summary>
		/// <param name="pKey"><b>REFPROPERTYKEY</b> that specifies the name of the property to retrieve.</param>
		/// <returns>Address of a <b>PROPVARIANT</b> that receives the property value.</returns>
		/// <remarks>
		/// <para>The properties may be platform defined or manufacturer defined.</para>
		/// <para>
		/// Platform-defined <b>PROPERTYKEY</b> values for location sensors are defined in the location sensor data types section of sensors.h.
		/// </para>
		/// <para>
		/// The following is a table of some platform-defined properties that are commonly association with location reports. These property
		/// keys have a <b>fmtid</b> field equal to <b>SENSOR_DATA_TYPE_LOCATION_GUID</b>. Additional properties can be found in sensors.h.
		/// If you are implementing your own location report to pass to <c>SetReport</c>, this table indicates which values must be supplied
		/// in your report object's implementation of <b>GetValue</b>.
		/// </para>
		/// <list type="table">
		/// <listheader>
		/// <description>Property key name and type</description>
		/// <description>Description</description>
		/// </listheader>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_LATITUDE <b>VT_R8</b></description>
		/// <description>Degrees latitude where North is positive.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_LONGITUDE <b>VT_R8</b></description>
		/// <description>Degrees longitude where East is positive.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_ALTITUDE_SEALEVEL_METERS <b>VT_R8</b></description>
		/// <description>Altitude with respect to sea level, in meters.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_ALTITUDE_ELLIPSOID_METERS <b>VT_R8</b></description>
		/// <description>Altitude with respect to the reference ellipsoid, in meters.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_SPEED_KNOTS <b>VT_R8</b></description>
		/// <description>Speed measured in knots.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_TRUE_HEADING_DEGREES <b>VT_R8</b></description>
		/// <description>Heading relative to true north in degrees.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_MAGNETIC_HEADING_DEGREES <b>VT_R8</b></description>
		/// <description>Heading relative to magnetic north in degrees.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_MAGNETIC_VARIATION <b>VT_R8</b></description>
		/// <description>Magnetic variation. East is positive.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_ERROR_RADIUS_METERS <b>VT_R8</b></description>
		/// <description>Error radius that indicates accuracy of latitude and longitude in meters.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_ADDRESS1 <b>VT_LPWSTR</b></description>
		/// <description>First line of the address in a civic address report.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_ADDRESS2 <b>VT_LPWSTR</b></description>
		/// <description>Second line of the address in a civic address report.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_CITY <b>VT_LPWSTR</b></description>
		/// <description>City field in a civic address report.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_STATE_PROVINCE <b>VT_LPWSTR</b></description>
		/// <description>State/province field in a civic address report.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_POSTALCODE <b>VT_LPWSTR</b></description>
		/// <description>Postal code field in a civic address report.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_COUNTRY_REGION <b>VT_LPWSTR</b></description>
		/// <description>
		/// Country/region code in a civic address report. The value must be a two-letter or three-letter ISO 3166 country code.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_ALTITUDE_ELLIPSOID_ERROR_METERS <b>VT_R8</b></description>
		/// <description>Altitude error with respect to the reference ellipsoid, in meters.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_ALTITUDE_SEALEVEL_ERROR_METERS <b>VT_R8</b></description>
		/// <description>Altitude error with respect to sea level, in meters.</description>
		/// </item>
		/// </list>
		/// <para></para>
		/// <para>
		/// The following is a table of other platform-defined properties that may occur in location reports but are not specific to
		/// location. The <b>fmtid</b> field of these property keys is <b>SENSOR_PROPERTY_COMMON_GUID</b>.
		/// </para>
		/// <list type="table">
		/// <listheader>
		/// <description>Property key name and type</description>
		/// <description>Description</description>
		/// </listheader>
		/// <item>
		/// <description>SENSOR_PROPERTY_ACCURACY <b>VT_UNKNOWN</b></description>
		/// <description>
		/// IPortableDeviceValues object that contains sensor data type names and their associated accuracies. Accuracy values represent
		/// possible variation from true values. Accuracy values are expressed by using the same units as the data field, except when
		/// otherwise documented.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_CHANGE_SENSITIVITY <b>VT_UNKNOWN</b></description>
		/// <description>
		/// <b>IPortableDeviceValues</b> object that contains sensor data type names and their associated change sensitivity values. Change
		/// sensitivity values represent the amount by which the data field must change before the SENSOR_EVENT_DATA_UPDATED event is raised.
		/// Sensitivity values are expressed by using the same units as the data field, except where otherwise documented. For example, a
		/// change sensitivity value of 2 for SENSOR_DATA_TYPE_TEMPERATURE_CELSIUS would represent a sensitivity of plus or minus 2 degrees Celsius.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_CURRENT_CONNECTION_TYPE <b>VT_UI4</b></description>
		/// <description><c>SensorConnectionType</c> value that contains the current connection type.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_CURRENT_REPORT_INTERVAL <b>VT_UI4</b></description>
		/// <description>
		/// The current elapsed time for sensor data report generation, in milliseconds. Setting a value of zero signals the driver to use
		/// its default report interval. After receiving a value of zero for this property, a driver must return its default report interval,
		/// not zero, when queried. Applications can set this value to request a particular report interval, but multiple applications might
		/// be using the same driver. Therefore, drivers determine the true report interval that is based on internal logic. For example, the
		/// driver might always use the shortest report interval that is requested by the caller.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_DESCRIPTION <b>VT_LPWSTR</b></description>
		/// <description>The sensor description string.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_DEVICE_PATH <b>VT_LPWSTR</b></description>
		/// <description>
		/// Uniquely identifies the device instance with which the sensor is associated. You can use this property to determine whether a
		/// device contains multiple sensors. Device drivers do not have to support this property because the platform provides this value to
		/// applications without querying drivers.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_FRIENDLY_NAME <b>VT_LPWSTR</b></description>
		/// <description>The friendly name for the device.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_LOCATION_DESIRED_ACCURACY <b>VT_UI4</b></description>
		/// <description>
		/// An enumerated value that indicates the type of accuracy handling requested by a client application.
		/// <b>LOCATION_DESIRED_ACCURACY_DEFAULT</b> (0) indicates that the sensor should use the accuracy for which it can optimize power
		/// use and other cost considerations. <b>LOCATION_DESIRED_ACCURACY_HIGH</b> (1) indicates that the sensor should deliver the most
		/// accurate report possible. This includes using services that might charge money, or consuming higher levels of battery power or
		/// connection bandwidth.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_MANUFACTURER <b>VT_LPWSTR</b></description>
		/// <description>The manufacturer's name.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_MIN_REPORT_INTERVAL <b>VT_UI4</b></description>
		/// <description>The minimum elapsed time setting that the hardware supports for sensor data report generation, in milliseconds.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_MODEL <b>VT_LPWSTR</b></description>
		/// <description>The sensor model name.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_PERSISTENT_UNIQUE_ID <b>VT_CLSID</b></description>
		/// <description>
		/// A <b>GUID</b> that identifies the sensor. This value must be unique for each sensor on a device, or across devices of the same
		/// model as enumerated on the computer.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_RANGE_MAXIMUM <b>VT_UKNOWN</b></description>
		/// <description><b>IPortableDeviceValues</b> object that contains sensor data field names and their associated maximum values.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_RANGE_MINIMUM <b>VT_UKNOWN</b></description>
		/// <description><b>IPortableDeviceValues</b> object that contains sensor data field names and their associated minimum values.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_RESOLUTION <b>VT_UKNOWN</b></description>
		/// <description>
		/// <b>IPortableDeviceValues</b> object that contains sensor data field names and their associated resolutions. Resolution values
		/// represent sensitivity to change in the data field. Resolution values are expressed by using the same units as the data field,
		/// except when otherwise documented.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_SERIAL_NUMBER <b>VT_LPWSTR</b></description>
		/// <description>The sensor serial number.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_STATE <b>VT_UI4</b></description>
		/// <description><c>SensorState</c> value that contains the current sensor state.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_TYPE <b>VT_CLSID</b></description>
		/// <description>A <b>GUID</b> that identifies the sensor type. Platform-defined sensor types are defined in Sensors.h.</description>
		/// </item>
		/// </list>
		/// <para></para>
		/// <para>Examples</para>
		/// <para>
		/// The following example demonstrates how to call <b>GetValue</b> to get a property value. You must include sensors.h to use the
		/// constant in the example.
		/// </para>
		/// <para><c>PROPVARIANT pv; HRESULT hr = spLatLongReport-&gt;GetValue(SENSOR_DATA_TYPE_LATITUDE_DEGREES, &amp;pv);</c></para>
		/// <para>
		/// The following example shows how to implement <b>GetValue</b> in your own report object. This implementation allows the caller to
		/// get values for several location report fields. This code requires you to include sensors.h and provarutil.h.
		/// </para>
		/// <para>
		/// <c>STDMETHODIMP CLocationReport::GetValue(REFPROPERTYKEY pKey, PROPVARIANT *pValue) { HRESULT hr = S_OK; if (pKey.fmtid ==
		/// SENSOR_DATA_TYPE_LOCATION_GUID) { // properties for civic address reports if (pKey.pid == SENSOR_DATA_TYPE_ADDRESS1.pid) { hr =
		/// InitPropVariantFromString(m_address1, pValue); } else if (pKey.pid == SENSOR_DATA_TYPE_ADDRESS2.pid) { hr =
		/// InitPropVariantFromString(m_address2, pValue); } else if (pKey.pid == SENSOR_DATA_TYPE_CITY.pid) { hr =
		/// InitPropVariantFromString(m_city, pValue); } else if (pKey.pid == SENSOR_DATA_TYPE_STATE_PROVINCE.pid) { hr =
		/// InitPropVariantFromString(m_stateprovince, pValue); } else if (pKey.pid == SENSOR_DATA_TYPE_POSTALCODE.pid) { hr =
		/// InitPropVariantFromString(m_postalcode, pValue); } else if (pKey.pid == SENSOR_DATA_TYPE_COUNTRY_REGION.pid) { hr =
		/// InitPropVariantFromString(m_countryregion, pValue); } // properties for latitude/longitude reports else if (pKey.pid ==
		/// SENSOR_DATA_TYPE_LATITUDE_DEGREES.pid) { hr = InitPropVariantFromDouble(m_latitude, pValue); } else if (pKey.pid ==
		/// SENSOR_DATA_TYPE_LONGITUDE_DEGREES.pid) { hr = InitPropVariantFromDouble(m_longitude, pValue); } else if (pKey.pid ==
		/// SENSOR_DATA_TYPE_ERROR_RADIUS_METERS.pid) { hr = InitPropVariantFromDouble(m_errorradius, pValue); } else { hr =
		/// HRESULT_FROM_WIN32(ERROR_NO_DATA); PropVariantInit(pValue); } } return hr; }</c>
		/// </para>
		/// </remarks>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilocationreport-getvalue HRESULT GetValue( [in]
		// REFPROPERTYKEY pKey, [out] PROPVARIANT *pValue );
		new PROPVARIANT GetValue(in PROPERTYKEY pKey);

		/// <summary>Retrieves the latitude, in degrees. The latitude is between -90 and 90, where north is positive.</summary>
		/// <returns>Address of a <b>DOUBLE</b> that receives the latitude.</returns>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilatlongreport-getlatitude HRESULT GetLatitude(
		// [out] DOUBLE *pLatitude );
		double GetLatitude();

		/// <summary>Retrieves the longitude, in degrees. The longitude is between -180 and 180, where East is positive.</summary>
		/// <returns>Address of a <b>DOUBLE</b> that receives the longitude.</returns>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilatlongreport-getlongitude HRESULT GetLongitude(
		// [out] DOUBLE *pLongitude );
		double GetLongitude();

		/// <summary>
		/// Retrieves a distance from the reported location, in meters. Combined with the location reported as the origin, this radius
		/// describes the circle in which the actual location is probably located.
		/// </summary>
		/// <returns>Address of a <b>DOUBLE</b> that receives the error radius.</returns>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilatlongreport-geterrorradius HRESULT
		// GetErrorRadius( [out] DOUBLE *pErrorRadius );
		double GetErrorRadius();

		/// <summary>Retrieves the altitude, in meters. Altitude is relative to the reference ellipsoid.</summary>
		/// <returns>Address of a <b>DOUBLE</b> that receives the altitude, in meters. May be <b>NULL</b>.</returns>
		/// <remarks>
		/// <para>
		/// The <b>GetAltitude</b> method retrieves the altitude relative to the reference ellipsoid that is defined by the latest revision
		/// of the World Geodetic System (WGS 84), rather than the altitude relative to sea level.
		/// </para>
		/// <para>Examples</para>
		/// <para>
		/// The following code example demonstrates how to call <b>GetAltitude</b>. Altitude is an optional field in latitude/longitude
		/// reports, so <b>GetAltitude</b> may not always return data.
		/// </para>
		/// <para>
		/// <c>DOUBLE altitude = 0; // Print the Altitude if (SUCCEEDED(spLatLongReport-&gt;GetAltitude(&amp;altitude))) {
		/// wprintf(L"Altitude: %f\n", altitude); } else { // Altitude is optional and may not be available wprintf(L"Altitude: Not
		/// available.\n"); }</c>
		/// </para>
		/// </remarks>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilatlongreport-getaltitude HRESULT GetAltitude(
		// [out] DOUBLE *pAltitude );
		double GetAltitude();

		/// <summary>Retrieves the altitude error, in meters.</summary>
		/// <returns>Address of a <b>DOUBLE</b> that receives the altitude error, in meters. May be <b>NULL</b>.</returns>
		// https://learn.microsoft.com/he-il/windows/win32/api/locationapi/nf-locationapi-ilatlongreport-getaltitudeerror HRESULT
		// GetAltitudeError( [out] DOUBLE *pAltitudeError );
		double GetAltitudeError();
	}

	/// <summary/>
	[ComImport, Guid("3F0804CB-B114-447D-83DD-390174EBB082"), InterfaceType(ComInterfaceType.InterfaceIsDual), CoClass(typeof(LatLongReportFactory))]
	public interface ILatLongReportFactory : ILocationReportFactory
	{
		/// <summary/>
		[PreserveSig]
		new HRESULT ListenForReports(uint requestedReportInterval = 0);

		/// <summary/>
		[PreserveSig]
		new HRESULT StopListeningForReports();

		/// <summary/>
		new uint Status { get; }

		/// <summary/>
		new uint ReportInterval { get; set; }

		/// <summary/>
		new uint DesiredAccuracy { get; set; }

		/// <summary/>
		[PreserveSig]
		new HRESULT RequestPermissions(in uint hWnd);

		/// <summary/>
		IDispLatLongReport? LatLongReport { get; }
	}

	/// <summary>Provides methods used to manage location reports, event registration, and sensor permissions.</summary>
	/// <remarks>
	/// When <b>CoCreateInstance</b> is called to create an <b>ILocation</b> object, it may result in a notification being displayed in the
	/// taskbar, and a Location Activity event being logged in Event Viewer, if it is the application's first use of location.
	/// </remarks>
	// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nn-locationapi-ilocation
	[PInvokeData("locationapi.h", MSDNShortId = "NN:locationapi.ILocation")]
	[ComImport, Guid("AB2ECE69-56D9-4F28-B525-DE1B0EE44237"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), CoClass(typeof(Location))]
	public interface ILocation
	{
		/// <summary>Requests location report events.</summary>
		/// <param name="pEvents">
		/// Pointer to the <c>ILocationEvents</c> callback interface through which the requested event notifications will be received.
		/// </param>
		/// <param name="reportType"><b>GUID</b> that specifies the interface ID of the report type for which to receive event notifications.</param>
		/// <param name="dwRequestedReportInterval">
		/// <b>DWORD</b> that specifies the requested elapsed time, in milliseconds, between event notifications for the specified report
		/// type. If <i>dwRequestedReportInterval</i> is zero, no minimum interval is specified and your application requests to receive
		/// events at the location sensor's default interval. See Remarks.
		/// </param>
		/// <returns>
		/// <para>The method returns an <b>HRESULT</b>. Possible values include, but are not limited to, those in the following table.</para>
		/// <list type="table">
		/// <listheader>
		/// <description>Return code</description>
		/// <description>Description</description>
		/// </listheader>
		/// <item>
		/// <description><b>S_OK</b></description>
		/// <description>The method succeeded.</description>
		/// </item>
		/// <item>
		/// <description><b>HRESULT_FROM_WIN32(ERROR_NOT_SUPPORTED)</b></description>
		/// <description><i>reportType</i> is other than IID_ILatLongReport or IID_ICivicAddressReport.</description>
		/// </item>
		/// <item>
		/// <description><b>HRESULT_FROM_WIN32(ERROR_ALREADY_REGISTERED)</b></description>
		/// <description><i>reportType</i> is already registered.</description>
		/// </item>
		/// </list>
		/// </returns>
		/// <remarks>
		/// <para>
		/// The interval you request by using the <i>dwRequestedReportInterval</i> parameter represents the shortest amount of time between
		/// events. This means that you request to receive event notifications no more frequently than specified, but the elapsed time may be
		/// significantly longer. Use the <i>dwRequestedReportInterval</i> parameter to help ensure that event notifications do not use more
		/// processor resources than necessary.
		/// </para>
		/// <para>
		/// The location provider is not required to provide reports at the interval that you request. Call <c>GetReportInterval</c> to
		/// discover the true report interval setting.
		/// </para>
		/// <para>
		/// Applications that need to get location data only once, to fill out a form or place the user's location on a map, should register
		/// for events and wait for the first report event as described in <c>Waiting For a Location Report</c>.
		/// </para>
		/// <para>Examples</para>
		/// <para>The following example calls <b>RegisterForReport</b> to subscribe to events.</para>
		/// <para>
		/// <c>#include &lt;windows.h&gt; #include &lt;atlbase.h&gt; #include &lt;atlcom.h&gt; #include &lt;LocationApi.h&gt; // This is the
		/// main Location API header #include "LocationCallback.h" // This is our callback interface that receives Location reports. class
		/// CInitializeATL : public CAtlExeModuleT&lt;CInitializeATL&gt;{}; CInitializeATL g_InitializeATL; // Initializes ATL for this
		/// application. This also does CoInitialize for us int wmain() { HRESULT hr = CoInitializeEx(NULL, COINIT_MULTITHREADED |
		/// COINIT_DISABLE_OLE1DDE);; if (SUCCEEDED(hr)) { CComPtr&lt;ILocation&gt; spLocation; // This is the main Location interface
		/// CComObject&lt;CLocationEvents&gt;* pLocationEvents = NULL; // This is our callback object for location reports IID REPORT_TYPES[]
		/// = { IID_ILatLongReport }; // Array of report types of interest. Other ones include IID_ICivicAddressReport hr =
		/// spLocation.CoCreateInstance(CLSID_Location); // Create the Location object if (SUCCEEDED(hr)) { hr =
		/// CComObject&lt;CLocationEvents&gt;::CreateInstance(&amp;pLocationEvents); // Create the callback object if (NULL !=
		/// pLocationEvents) { pLocationEvents-&gt;AddRef(); } } if (SUCCEEDED(hr)) { // Request permissions for this user account to receive
		/// location data for all the // types defined in REPORT_TYPES (which is currently just one report) if
		/// (FAILED(spLocation-&gt;RequestPermissions(NULL, REPORT_TYPES, ARRAYSIZE(REPORT_TYPES), FALSE))) // FALSE means an asynchronous
		/// request { wprintf(L"Warning: Unable to request permissions.\n"); } // Tell the Location API that we want to register for reports
		/// (which is currently just one report) for (DWORD index = 0; index &lt; ARRAYSIZE(REPORT_TYPES); index++) { hr =
		/// spLocation-&gt;RegisterForReport(pLocationEvents, REPORT_TYPES[index], 0); } } if (SUCCEEDED(hr)) { // Wait until user presses a
		/// key to exit app. During this time the Location API // will send reports to our callback interface on another thread.
		/// system("pause"); // Unregister from reports from the Location API for (DWORD index = 0; index &lt; ARRAYSIZE(REPORT_TYPES);
		/// index++) { spLocation-&gt;UnregisterForReport(REPORT_TYPES[index]); } } // Cleanup if (NULL != pLocationEvents) {
		/// pLocationEvents-&gt;Release(); pLocationEvents = NULL; } CoUninitialize(); } return 0; }</c>
		/// </para>
		/// </remarks>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilocation-registerforreport HRESULT
		// RegisterForReport( [in] ILocationEvents *pEvents, [in] REFIID reportType, [in] DWORD dwRequestedReportInterval );
		[PreserveSig]
		HRESULT RegisterForReport([In, Optional] ILocationEvents? pEvents, in Guid reportType, uint dwRequestedReportInterval);

		/// <summary>Stops event notifications for the specified report type.</summary>
		/// <param name="reportType"><b>REFIID</b> that specifies the interface ID of the report type for which to stop events.</param>
		/// <returns>
		/// <para>The method returns an <b>HRESULT</b>. Possible values include, but are not limited to, those in the following table.</para>
		/// <list type="table">
		/// <listheader>
		/// <description>Return code</description>
		/// <description>Description</description>
		/// </listheader>
		/// <item>
		/// <description><b>S_OK</b></description>
		/// <description>The method succeeded.</description>
		/// </item>
		/// <item>
		/// <description><b>HRESULT_FROM_WIN32(ERROR_INVALID_STATE)</b></description>
		/// <description>The caller is not registered to receive events for the specified report type.</description>
		/// </item>
		/// <item>
		/// <description><b>HRESULT_FROM_WIN32(ERROR_NOT_SUPPORTED)</b></description>
		/// <description><i>reportType</i> is other than IID_ILatLongReport or IID_ICivicAddressReport.</description>
		/// </item>
		/// </list>
		/// </returns>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilocation-unregisterforreport HRESULT
		// UnregisterForReport( [in] REFIID reportType );
		[PreserveSig]
		HRESULT UnregisterForReport(in Guid reportType);

		/// <summary>Retrieves a location report.</summary>
		/// <param name="reportType"><b>REFIID</b> that specifies the type of report to retrieve.</param>
		/// <returns>Address of a pointer to <c>ILocationReport</c> that receives the specified location report.</returns>
		/// <remarks>
		/// <para>
		/// <c>ILocationReport</c> is the base interface for specific location report types. Call <b>QueryInterface</b> to retrieve a pointer
		/// to the correct report type.
		/// </para>
		/// <para>
		/// When <b>GetReport</b> is called, it may result in a notification being displayed in the taskbar, and a Location Activity event
		/// being logged in Event Viewer, if it is the application's first use of location.
		/// </para>
		/// <para>
		/// <b>Note</b>  When an application first starts, or when a new location sensor is enabled, <c>GetReportStatus</c> may report a
		/// status of <b>REPORT_RUNNING</b> shortly before the new location report is available. Therefore, an initial call to
		/// <b>GetReport</b> can return an error ( <b>ERROR_NO_DATA</b>) or a value that is not from the expected location sensor, even if
		/// <b>GetReportStatus</b> indicates a status of <b>REPORT_RUNNING</b>. See <c>GetReportStatus</c> for a description of a workaround
		/// for this issue.
		/// </para>
		/// <para></para>
		/// <para>Examples</para>
		/// <para>
		/// The following example calls <b>GetReport</b> for latitude/longitude reports and demonstrates how to call QueryInterface to
		/// retrieve a pointer to the specified report type.
		/// </para>
		/// <para>
		/// <c>CComPtr&lt;ILocationReport&gt; spLocationReport; // This is our location report object CComPtr&lt;ILatLongReport&gt;
		/// spLatLongReport; // This is our LatLong report object // Get the current latitude/longitude location report, hr =
		/// spLocation-&gt;GetReport(IID_ILatLongReport, &amp;spLocationReport); // then get a pointer to the ILatLongReport interface by
		/// calling QueryInterface if (SUCCEEDED(hr)) { hr = spLocationReport-&gt;QueryInterface(&amp;spLatLongReport); }</c>
		/// </para>
		/// </remarks>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilocation-getreport HRESULT GetReport( [in] REFIID
		// reportType, [out] ILocationReport **ppLocationReport );
		ILocationReport? GetReport(in Guid reportType);

		/// <summary>Retrieves the status for the specified report type.</summary>
		/// <param name="reportType"><b>REFIID</b> that specifies the report type for which to get the interval.</param>
		/// <returns>Address of a <c>LOCATION_REPORT_STATUS</c> that receives the current status for the specified report.</returns>
		/// <remarks>
		/// <para>
		/// This method retrieves report status for new reports. The most recent reports remain available through
		/// <c>ILocation::GetReport</c>, regardless of the status reported by this method.
		/// </para>
		/// <para><c></c><c></c><c></c> Known Issues</para>
		/// <para>
		/// When an application first starts, or when a new location sensor is enabled, <b>GetReportStatus</b> may report a status of
		/// <b>REPORT_RUNNING</b> shortly before the location report is available.
		/// </para>
		/// <para>
		/// Therefore, an initial call to <c>GetReport</c> will return an error ( <b>ERROR_NO_DATA</b>) or a value that is not from the
		/// expected location sensor, even if <b>GetReportStatus</b> indicates a status of <b>REPORT_RUNNING</b>. This can happen in the
		/// following cases:
		/// </para>
		/// <list type="number">
		/// <item>
		/// <description>
		/// The application polls for status by using <b>GetReportStatus</b> until a report status of <b>REPORT_RUNNING</b> is returned, and
		/// then calls <c>GetReport</c>.
		/// </description>
		/// </item>
		/// <item>
		/// <description>
		/// <b>GetReportStatus</b> is called when the application starts. This may occur after creation of the location object, or after
		/// calling <c>RequestPermissions</c>.
		/// </description>
		/// </item>
		/// </list>
		/// <para>
		/// An application can mitigate the issue by implementing the following workaround. The workaround involves subscribing to location
		/// report events.
		/// </para>
		/// <para><c></c><c></c><c></c> Workaround: Subscribing to Events</para>
		/// <para>
		/// The application can subscribe to report events and wait for the report from the <c>OnLocationChanged</c> event or the
		/// <c>OnStatusChanged</c> event. The application should wait for a specified finite amount of time.
		/// </para>
		/// <para>
		/// The following example shows an application that waits for a location report of type <c>ILatLongReport</c>. If a report is
		/// successfully retrieved within the specified amount of time, it prints out a message indicating that data was received.
		/// </para>
		/// <para>
		/// The following example code demonstrates how an application may call a function named <b>WaitForLocationReport</b> that registers
		/// for events and waits for the first location report. <b>WaitForLocationReport</b> waits for an event that is set by a callback
		/// object. The function <b>WaitForLocationReport</b> and the callback object is defined in the examples that follow this one.
		/// </para>
		/// <para>
		/// <c>// main.cpp // An application that demonstrates how to wait for a location report. // This sample waits for latitude/longitude
		/// reports but can be modified // to wait for civic address reports by replacing IID_ILatLongReport // with IID_ICivicAddressReport
		/// in the following code. #include "WaitForLocationReport.h" #define DEFAULT_WAIT_FOR_LOCATION_REPORT 500 // Wait for half a second.
		/// int wmain() { // You may use the flags COINIT_MULTITHREADED | COINIT_DISABLE_OLE1DDE // to specify the multi-threaded concurrency
		/// model. HRESULT hr = ::CoInitializeEx(NULL, COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE); if (SUCCEEDED(hr)) { int args;
		/// PWSTR *pszArgList = ::CommandLineToArgvW(::GetCommandLineW(), &amp;args); DWORD const dwTimeToWait = (2 == args) ?
		/// static_cast&lt;DWORD&gt;(_wtoi(pszArgList[1])) : DEFAULT_WAIT_FOR_LOCATION_REPORT; ::LocalFree(pszArgList); wprintf_s(L"Wait time
		/// set to %lu\n", dwTimeToWait); ILocation *pLocation; // This is the main Location interface. hr = CoCreateInstance(CLSID_Location,
		/// NULL, CLSCTX_INPROC, IID_PPV_ARGS(&amp;pLocation)); if (SUCCEEDED(hr)) { // Array of report types to listen for. // Replace
		/// IID_ILatLongReport with IID_ICivicAddressReport // for civic address reports. IID REPORT_TYPES[] = { IID_ILatLongReport }; //
		/// Request permissions for this user account to receive location data for all the // types defined in REPORT_TYPES (which is
		/// currently just one report) // TRUE means a synchronous request. if (FAILED(pLocation-&gt;RequestPermissions(NULL, REPORT_TYPES,
		/// ARRAYSIZE(REPORT_TYPES), TRUE))) { wprintf_s(L"Warning: Unable to request permissions.\n"); } ILocationReport *pLocationReport;
		/// // This is our location report object // Replace IID_ILatLongReport with IID_ICivicAddressReport for civic address reports hr =
		/// ::WaitForLocationReport(pLocation, IID_ILatLongReport, dwTimeToWait, &amp;pLocationReport); if (SUCCEEDED(hr)) {
		/// wprintf_s(L"Successfully received data via GetReport().\n"); pLocationReport-&gt;Release(); } else if (RPC_S_CALLPENDING == hr) {
		/// wprintf_s(L"No LatLong data received. Wait time of %lu elapsed.\n", dwTimeToWait); } pLocation-&gt;Release(); }
		/// ::CoUninitialize(); } return 0; }</c>
		/// </para>
		/// <para>
		/// The following example code is separated into WaitForLocationReport.h and WaitForLocationReport.cpp. WaitForLocationReport.h
		/// contains the header for the <b>WaitForLocationReport</b> function. WaitForLocationReport.cpp contains the definition of the
		/// <b>WaitForLocationReport</b> function and the definition of the callback object that it uses. The callback object provides
		/// implementations of the <c>OnLocationChanged</c> and <c>OnStatusChanged</c> callback methods. Within these methods, it sets an
		/// event that signals when a report is available.
		/// </para>
		/// <para>
		/// <c>// WaitForLocationReport.h // Header for the declaration of the WaitForLocationReport function. #pragma once #include
		/// &lt;windows.h&gt; #include &lt;LocationApi.h&gt; #include &lt;wchar.h&gt; HRESULT WaitForLocationReport( ILocation* pLocation, //
		/// Location object. REFIID reportType, // Type of report. DWORD dwTimeToWait, // Milliseconds to wait. ILocationReport**
		/// ppLocationReport // Receives the location report. );</c>
		/// </para>
		/// <para>
		/// <c>// WaitForLocationReport.cpp // Contains definitions of the WaitForLocationReport function and // the callback object that it
		/// uses. #include "WaitForLocationReport.h" #include &lt;shlwapi.h&gt; #include &lt;new&gt; // Implementation of the callback
		/// interface that receives location reports. class CLocationCallback : public ILocationEvents { public: CLocationCallback() :
		/// _cRef(1), _hDataEvent(::CreateEvent( NULL, // Default security attributes. FALSE, // Auto-reset event. FALSE, // Initial state is
		/// nonsignaled. NULL)) // No event name. { } virtual ~CLocationCallback() { if (_hDataEvent) { ::CloseHandle(_hDataEvent); } }
		/// IFACEMETHODIMP QueryInterface(REFIID riid, void **ppv) { if ((riid == IID_IUnknown) || (riid == IID_ILocationEvents)) { *ppv =
		/// static_cast&lt;ILocationEvents*&gt;(this); } else { *ppv = NULL; return E_NOINTERFACE; } AddRef(); return S_OK; }
		/// IFACEMETHODIMP_(ULONG) AddRef() { return InterlockedIncrement(&amp;_cRef); } IFACEMETHODIMP_(ULONG) Release() { long cRef =
		/// InterlockedDecrement(&amp;_cRef); if (!cRef) { delete this; } return cRef; } // ILocationEvents // This is called when there is a
		/// new location report. IFACEMETHODIMP OnLocationChanged(REFIID /*reportType*/, ILocationReport* /*pLocationReport*/) {
		/// ::SetEvent(_hDataEvent); return S_OK; } // This is called when the status of a report type changes. // The LOCATION_REPORT_STATUS
		/// enumeration is defined in LocApi.h in the SDK IFACEMETHODIMP OnStatusChanged(REFIID /*reportType*/, LOCATION_REPORT_STATUS
		/// status) { if (REPORT_RUNNING == status) { ::SetEvent(_hDataEvent); } return S_OK; } HANDLE GetEventHandle() { return _hDataEvent;
		/// } private: long _cRef; HANDLE _hDataEvent; // Data Event Handle }; // Waits to receive a location report. // This function waits
		/// for the callback object to signal when // a report event or status event occurs, and then calls GetReport. // Even if no report
		/// event or status event is received before the timeout, // this function still queries for the last known report by calling
		/// GetReport. // The last known report may be cached data from a location sensor that is not // reporting events, or data from the
		/// default location provider. // // Returns S_OK if the location report has been returned // or RPC_S_CALLPENDING if the timeout
		/// expired. HRESULT WaitForLocationReport( ILocation* pLocation, // Location object. REFIID reportType, // Type of report to wait
		/// for. DWORD dwTimeToWait, // Milliseconds to wait. ILocationReport **ppLocationReport // Receives the location report. ) {
		/// *ppLocationReport = NULL; CLocationCallback *pLocationCallback = new(std::nothrow) CLocationCallback(); HRESULT hr =
		/// pLocationCallback ? S_OK : E_OUTOFMEMORY; if (SUCCEEDED(hr)) { HANDLE hEvent = pLocationCallback-&gt;GetEventHandle(); hr =
		/// hEvent ? S_OK : E_FAIL; if (SUCCEEDED(hr)) { // Tell the Location API that we want to register for a report. hr =
		/// pLocation-&gt;RegisterForReport(pLocationCallback, reportType, 0); if (SUCCEEDED(hr)) { DWORD dwIndex; HRESULT hrWait =
		/// CoWaitForMultipleHandles(0, dwTimeToWait, 1, &amp;hEvent, &amp;dwIndex); if ((S_OK == hrWait) || (RPC_S_CALLPENDING == hrWait)) {
		/// // Even if there is a timeout indicated by RPC_S_CALLPENDING // attempt to query the report to return the last known report. hr =
		/// pLocation-&gt;GetReport(reportType, ppLocationReport); if (FAILED(hr) &amp;&amp; (RPC_S_CALLPENDING == hrWait)) { // Override hr
		/// error if the request timed out and // no data is available from the last known report. hr = hrWait; // RPC_S_CALLPENDING } } //
		/// Unregister from reports from the Location API. pLocation-&gt;UnregisterForReport(reportType); } }
		/// pLocationCallback-&gt;Release(); } return hr; }</c>
		/// </para>
		/// </remarks>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilocation-getreportstatus HRESULT GetReportStatus(
		// [in] REFIID reportType, [out] LOCATION_REPORT_STATUS *pStatus );
		LOCATION_REPORT_STATUS GetReportStatus(in Guid reportType);

		/// <summary>Retrieves the requested amount of time, in milliseconds, between report events.</summary>
		/// <param name="reportType"><b>REFIID</b> that specifies the report type for which to get the interval.</param>
		/// <returns>
		/// The address of a <b>DWORD</b> that receives the report interval value, in milliseconds. If the report is not registered, this
		/// will be set to <b>NULL</b>. If this value is set to zero, no minimum interval is specified and your application receives events
		/// at the location sensor's default interval.
		/// </returns>
		/// <remarks>
		/// <para>You must call <c>RegisterForReport</c> before calling this method.</para>
		/// <para>Examples</para>
		/// <para>The following example demonstrates how to call <b>GetReportInterval</b>.</para>
		/// <para><c>DWORD reportInterval = 0; HRESULT hr = spLocation-&gt;GetReportInterval(IID_ILatLongReport, &amp;reportInterval);</c></para>
		/// </remarks>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilocation-getreportinterval HRESULT
		// GetReportInterval( [in] REFIID reportType, [out] DWORD *pMilliseconds );
		uint GetReportInterval(in Guid reportType);

		/// <summary>Specifies the requested minimum amount of time, in milliseconds, between report events.</summary>
		/// <param name="reportType"><b>REFIID</b> that specifies the report type for which to set the interval.</param>
		/// <param name="millisecondsRequested">
		/// <b>DWORD</b> that contains the report interval value, in milliseconds. If this value is zero, no minimum interval is specified
		/// and your application receives events at the location sensor's default interval.
		/// </param>
		/// <remarks>
		/// <para>
		/// The interval you request by using this method represents the shortest amount of time between events. This means that you request
		/// to receive event notifications no more frequently than specified, but the elapsed time may be significantly longer. Use this
		/// method to help ensure that event notifications do not use more processor resources than necessary.
		/// </para>
		/// <para>
		/// It is not guaranteed that your request for a particular report interval will be set by the location provider. Call
		/// <c>GetReportInterval</c> to discover the true report interval setting.
		/// </para>
		/// <para>
		/// A report interval of zero means that no minimum interval is specified, and the application may receive events at the frequency
		/// that the location sensor sends events.
		/// </para>
		/// <para>Examples</para>
		/// <para>The following example demonstrates how to call <b>SetReportInterval</b>.</para>
		/// <para>
		/// <c>// Set the latitude/longitude report interval to 1000 milliseconds HRESULT hr =
		/// spLocation-&gt;SetReportInterval(IID_ILatLongReport, 1000);</c>
		/// </para>
		/// </remarks>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilocation-setreportinterval HRESULT
		// SetReportInterval( [in] REFIID reportType, [in] DWORD millisecondsRequested );
		void SetReportInterval(in Guid reportType, uint millisecondsRequested);

		/// <summary>Retrieves the current requested accuracy setting.</summary>
		/// <param name="reportType"><b>REFIID</b> that specifies the report type for which to get the requested accuracy.</param>
		/// <returns>
		/// The address of a <c>LOCATION_DESIRED_ACCURACY</c> that receives the accuracy value. If the report is not registered, this will be
		/// set to <b>NULL</b>.
		/// </returns>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilocation-getdesiredaccuracy HRESULT
		// GetDesiredAccuracy( [in] REFIID reportType, [out] LOCATION_DESIRED_ACCURACY *pDesiredAccuracy );
		LOCATION_DESIRED_ACCURACY GetDesiredAccuracy(in Guid reportType);

		/// <summary>Specifies the accuracy to be used.</summary>
		/// <param name="reportType"><b>REFIID</b> that specifies the report type for which to set the accuracy to be used.</param>
		/// <param name="desiredAccuracy"><c>LOCATION_DESIRED_ACCURACY</c> value that specifies the accuracy to be used.</param>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilocation-setdesiredaccuracy HRESULT
		// SetDesiredAccuracy( [in] REFIID reportType, [in] LOCATION_DESIRED_ACCURACY desiredAccuracy );
		void SetDesiredAccuracy(in Guid reportType, LOCATION_DESIRED_ACCURACY desiredAccuracy);

		/// <summary>Opens a system dialog box to request user permission to enable location devices.</summary>
		/// <param name="hParent">
		/// <b>HWND</b> for the parent window. This parameter is optional. In Windows 8 the dialog is always modal if <i>hParent</i> is
		/// provided, and not modal if <i>hParent</i> is NULL.
		/// </param>
		/// <param name="pReportTypes">
		/// Pointer to an <b>IID</b> array. This array must contain interface IDs for all report types for which you are requesting
		/// permission. The interface IDs of the valid report types are IID_ILatLongReport and IID_ICivicAddressReport. The count of IDs must
		/// match the value specified through the <i>count</i> parameter.
		/// </param>
		/// <param name="count">The count of interface IDs contained in <i>pReportTypes</i>.</param>
		/// <param name="fModal">This parameter is not used.</param>
		/// <returns>
		/// <para>This method can return one of these values.</para>
		/// <para>The following table describes return codes when the call is synchronous.</para>
		/// <list type="table">
		/// <listheader>
		/// <description>Return code</description>
		/// <description>Description</description>
		/// </listheader>
		/// <item>
		/// <description><b>S_OK</b></description>
		/// <description>The user enabled location services. The method succeeded.</description>
		/// </item>
		/// <item>
		/// <description><b>HRESULT_FROM_WIN32(ERROR_ACCESS_DENIED)</b></description>
		/// <description>The location platform is disabled. An administrator turned the location platform off.</description>
		/// </item>
		/// <item>
		/// <description><b>HRESULT_FROM_WIN32(ERROR_CANCELLED)</b></description>
		/// <description>The user did not enable access to location services or canceled the dialog box.</description>
		/// </item>
		/// </list>
		/// <para></para>
		/// <para>The following table describes return codes when the call is asynchronous.</para>
		/// <list type="table">
		/// <listheader>
		/// <description>Return code</description>
		/// <description>Description</description>
		/// </listheader>
		/// <item>
		/// <description><b>S_OK</b></description>
		/// <description>The user enabled access to location services. The method succeeded.</description>
		/// </item>
		/// <item>
		/// <description><b>E_INVALIDARG</b></description>
		/// <description>An argument is not valid.</description>
		/// </item>
		/// <item>
		/// <description><b>HRESULT_FROM_WIN32(ERROR_ACCESS_DENIED)</b></description>
		/// <description>
		/// The location platform is disabled. An administrator turned the location platform off. The dialog box was not shown.
		/// </description>
		/// </item>
		/// </list>
		/// </returns>
		/// <remarks>
		/// <para>If the user chooses not to enable location services, Windows will not show the permissions dialog box again.</para>
		/// <para>
		/// <b>Note</b>  Repeated asynchronous calls to <b>RequestPermissions</b> will display multiple instances of the <b>Enable location
		/// services</b> dialog box and can potentially flood the screen with dialog boxes, resulting in a poor user experience. If you think
		/// that other location sensors might be installed after your first call to <b>RequestPermissions</b>, requiring another call to
		/// <b>RequestPermissions</b>, you should either call <b>RequestPermissions</b> synchronously or wait until all location sensors are
		/// installed to make an asynchronous call.
		/// </para>
		/// <para></para>
		/// <para>
		/// <b>Note</b>  Making a synchronous call from the user interface (UI) thread of a Windows application can block the UI thread and
		/// make the application less responsive. To prevent this, do not make a synchronous call to <b>RequestPermissions</b> from the UI thread.
		/// </para>
		/// <para></para>
		/// <para>
		/// <b>Note</b>  If an application running in protected mode, such as a Browser Helper Object (BHO) for Internet Explorer, calls
		/// <b>RequestPermissions</b>, and the user chooses not to enable location using the dialog box, the location provider will not be
		/// enabled, but Windows will display the dialog box again if <b>RequestPermissions</b> is called again by the same user.
		/// </para>
		/// <para></para>
		/// <para>Examples</para>
		/// <para>The following example demonstrates how to call <b>RequestPermissions</b> to request permission for latitude/longitude reports.</para>
		/// <para>
		/// <c>// Array of report types of interest. Other ones include IID_ICivicAddressReport IID REPORT_TYPES[] = { IID_ILatLongReport };
		/// // Request permissions for this user account to receive location data for all the // types defined in REPORT_TYPES (which is
		/// currently just one report type) // The last parameter is not used. if (FAILED(spLocation-&gt;RequestPermissions( NULL,
		/// REPORT_TYPES, ARRAYSIZE(REPORT_TYPES), TRUE))) { wprintf(L"Warning: Unable to request permissions.\n"); }</c>
		/// </para>
		/// </remarks>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilocation-requestpermissions HRESULT
		// RequestPermissions( [in] HWND hParent, [in] IID *pReportTypes, [in] ULONG count, BOOL fModal );
		[PreserveSig]
		HRESULT RequestPermissions([In, Optional] HWND hParent, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] Guid[] pReportTypes,
			uint count, [MarshalAs(UnmanagedType.Bool)] bool fModal);
	}

	/// <summary><b>ILocationEvents</b> provides callback methods that you must implement if you want to receive event notifications.</summary>
	// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nn-locationapi-ilocationevents
	[PInvokeData("locationapi.h", MSDNShortId = "NN:locationapi.ILocationEvents")]
	[ComImport, Guid("CAE02BBF-798B-4508-A207-35A7906DC73D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface ILocationEvents
	{
		/// <summary>Called when a new location report is available.</summary>
		/// <param name="reportType"><b>REFIID</b> that contains the interface ID of the report type contained in <i>pLocationReport</i>.</param>
		/// <param name="pLocationReport">Pointer to the <c>ILocationReport</c> instance that contains the new location report.</param>
		/// <returns>If this method succeeds, it returns <b>S_OK</b>. Otherwise, it returns an <b>HRESULT</b> error code.</returns>
		/// <remarks>
		/// <para>
		/// <c>ILocationReport</c> is the base interface of specific location report types. The actual interface that the caller receives for
		/// <i>pLocationReport</i> will match the type specified by <i>reportType</i>.
		/// </para>
		/// <para>
		/// If the application calls <b>OnLocationChanged</b> as a result of its first use of location, the call might cause a notification
		/// to appear in the taskbar, and cause a Location Activity event to be logged in Event Viewer.
		/// </para>
		/// <para>
		/// <b>Note</b>  An application does not receive the expected location change event from <b>OnLocationChanged</b> if both of the
		/// following conditions are true. First, the application runs as a service, in the context of the LOCALSERVICE, SYSTEM, or
		/// NETWORKSERVICE user account. Second, the location change event results from changing the default location, either manually when
		/// the user selects <b>Default Location</b> in Control Panel, or programmatically when an application calls <c>IDefaultLocation::SetReport</c>.
		/// </para>
		/// <para></para>
		/// <para>Examples</para>
		/// <para>
		/// The following sample implementation of <b>OnLocationChanged</b> handles the location change event for a latitude/longitude
		/// report. This implementation prints the following information about a latitude/longitude location change event: the timestamp, the
		/// sensor ID, the latitude, the longitude, the error radius, the altitude, and the altitude error.
		/// </para>
		/// <para>
		/// <c>// This is called when there is a new location report STDMETHODIMP CLocationEvents::OnLocationChanged(REFIID reportType,
		/// ILocationReport* pLocationReport) { // If the report type is a Latitude/Longitude report (as opposed to IID_ICivicAddressReport
		/// or another type) if (IID_ILatLongReport == reportType) { CComPtr&lt;ILatLongReport&gt; spLatLongReport; // Get the ILatLongReport
		/// interface from ILocationReport if ((SUCCEEDED(pLocationReport-&gt;QueryInterface(IID_PPV_ARGS(&amp;spLatLongReport)))) &amp;&amp;
		/// (NULL != spLatLongReport.p)) { // Print the Report Type GUID wchar_t szGUID[64]; wprintf(L"\nReportType: %s",
		/// GUIDToString(IID_ILatLongReport, szGUID, ARRAYSIZE(szGUID))); // Print the Timestamp and the time since the last report
		/// SYSTEMTIME systemTime; if (SUCCEEDED(spLatLongReport-&gt;GetTimestamp(&amp;systemTime))) { // Compute the number of 100ns units
		/// that difference between the current report's time and the previous report's time. ULONGLONG currentTime = 0, diffTime = 0; if
		/// (TRUE == SystemTimeToFileTime(&amp;systemTime, (FILETIME*)&amp;currentTime)) { diffTime = (currentTime &gt; m_previousTime) ?
		/// (currentTime - m_previousTime) : 0; } wprintf(L"\nTimestamp: YY:%d, MM:%d, DD:%d, HH:%d, MM:%d, SS:%d, MS:%d [%I64d]\n",
		/// systemTime.wYear, systemTime.wMonth, systemTime.wDay, systemTime.wHour, systemTime.wMinute, systemTime.wSecond,
		/// systemTime.wMilliseconds, diffTime / 10000); // Display in milliseconds m_previousTime = currentTime; // Set the previous time to
		/// the current time for the next report. } // Print the Sensor ID GUID GUID sensorID = {0}; if
		/// (SUCCEEDED(spLatLongReport-&gt;GetSensorID(&amp;sensorID))) { wchar_t szGUID[64]; wprintf(L"SensorID: %s\n",
		/// GUIDToString(sensorID, szGUID, ARRAYSIZE(szGUID))); } DOUBLE latitude = 0, longitude = 0, altitude = 0, errorRadius = 0,
		/// altitudeError = 0; // Print the Latitude if (SUCCEEDED(spLatLongReport-&gt;GetLatitude(&amp;latitude))) { wprintf(L"Latitude:
		/// %f\n", latitude); } // Print the Longitude if (SUCCEEDED(spLatLongReport-&gt;GetLongitude(&amp;longitude))) {
		/// wprintf(L"Longitude: %f\n", longitude); } // Print the Altitude if (SUCCEEDED(spLatLongReport-&gt;GetAltitude(&amp;altitude))) {
		/// wprintf(L"Altitude: %f\n", altitude); } else { // Altitude is optional and may not be available wprintf(L"Altitude: Not
		/// available.\n"); } // Print the Error Radius if (SUCCEEDED(spLatLongReport-&gt;GetErrorRadius(&amp;errorRadius))) {
		/// wprintf(L"Error Radius: %f\n", errorRadius); } // Print the Altitude Error if
		/// (SUCCEEDED(spLatLongReport-&gt;GetAltitudeError(&amp;altitudeError))) { wprintf(L"Altitude Error: %f\n", altitudeError); } else {
		/// // Altitude Error is optional and may not be available wprintf(L"Altitude Error: Not available.\n"); } } } return S_OK; }</c>
		/// </para>
		/// </remarks>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilocationevents-onlocationchanged HRESULT
		// OnLocationChanged( [in] REFIID reportType, [in] ILocationReport *pLocationReport );
		[PreserveSig]
		HRESULT OnLocationChanged(in Guid reportType, [In, Optional] ILocationReport? pLocationReport);

		/// <summary>Called when a report status changes.</summary>
		/// <param name="reportType"><b>REFIID</b> that specifies the interface ID of the report type for which the status has changed.</param>
		/// <param name="newStatus">A constant from the <c>LOCATION_REPORT_STATUS</c> enumeration that contains the new status.</param>
		/// <returns>If this method succeeds, it returns <b>S_OK</b>. Otherwise, it returns an <b>HRESULT</b> error code.</returns>
		/// <remarks>
		/// <para>
		/// This event provides report status for new reports. The most recent reports remain available through <c>ILocation::GetReport</c>,
		/// regardless of the status reported by this event.
		/// </para>
		/// <para>Examples</para>
		/// <para>
		/// The following is a sample implementation of <b>OnStatusChanged</b> that handles status changed events for latitude/longitude reports.
		/// </para>
		/// <para>
		/// <c>// This is called when the status of a report type changes. // The LOCATION_REPORT_STATUS enumeration is defined in LocApi.h
		/// in the SDK STDMETHODIMP CLocationEvents::OnStatusChanged(REFIID reportType, LOCATION_REPORT_STATUS status) { if
		/// (IID_ILatLongReport == reportType) { switch (status) { case REPORT_NOT_SUPPORTED: wprintf(L"\nNo devices detected.\n"); break;
		/// case REPORT_ERROR: wprintf(L"\nReport error.\n"); break; case REPORT_ACCESS_DENIED: wprintf(L"\nAccess denied to reports.\n");
		/// break; case REPORT_INITIALIZING: wprintf(L"\nReport is initializing.\n"); break; case REPORT_RUNNING: wprintf(L"\nRunning.\n");
		/// break; } } else if (IID_ICivicAddressReport == reportType) { } return S_OK; }</c>
		/// </para>
		/// </remarks>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilocationevents-onstatuschanged HRESULT
		// OnStatusChanged( [in] REFIID reportType, [in] LOCATION_REPORT_STATUS newStatus );
		[PreserveSig]
		HRESULT OnStatusChanged(in Guid reportType, LOCATION_REPORT_STATUS newStatus);
	}

	/// <summary>
	/// <para>
	/// Used by Windows Store app browsers in Windows 8 to notify the location platform that an app has been suspended (disconnect) and
	/// restored (connect).
	/// </para>
	/// <para>Most apps will not need to use this interface.</para>
	/// </summary>
	// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nn-locationapi-ilocationpower
	[PInvokeData("locationapi.h", MSDNShortId = "NN:locationapi.ILocationPower")]
	[ComImport, Guid("193E7729-AB6B-4b12-8617-7596E1BB191C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface ILocationPower
	{
		/// <summary>
		/// <para>
		/// Used by Windows Store app browsers in Windows 8 to notify the location platform that an app has been suspended (disconnect) and
		/// restored (connect).
		/// </para>
		/// <para>Most apps will not need to use this method.</para>
		/// </summary>
		/// <returns>If this method succeeds, it returns <b>S_OK</b>. Otherwise, it returns an <b>HRESULT</b> error code.</returns>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilocationpower-connect HRESULT Connect();
		[PreserveSig]
		HRESULT Connect();

		/// <summary>
		/// <para>
		/// Used by Windows Store app browsers in Windows 8 to notify the location platform that an app has been suspended (disconnect) and
		/// restored (connect).
		/// </para>
		/// <para>Most apps will not need to use this method.</para>
		/// </summary>
		/// <returns>If this method succeeds, it returns <b>S_OK</b>. Otherwise, it returns an <b>HRESULT</b> error code.</returns>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilocationpower-disconnect HRESULT Disconnect();
		[PreserveSig]
		HRESULT Disconnect();
	}

	/// <summary>The parent interface for location reports.</summary>
	// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nn-locationapi-ilocationreport
	[PInvokeData("locationapi.h", MSDNShortId = "NN:locationapi.ILocationReport")]
	[ComImport, Guid("C8B7F7EE-75D0-4db9-B62D-7A0F369CA456"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface ILocationReport
	{
		/// <summary>Retrieves the ID of the sensor that generated the location report.</summary>
		/// <returns>Address of a <b>SENSOR_ID</b> that receives the ID of the sensor that generated the location report.</returns>
		/// <remarks>
		/// <para>A sensor ID is a <b>GUID</b>.</para>
		/// <para>Examples</para>
		/// <para>The following example demonstrates how to call <b>GetSensorID</b>.</para>
		/// <para>
		/// <c>// Print the Sensor ID GUID GUID sensorID = {0}; if (SUCCEEDED(spLatLongReport-&gt;GetSensorID(&amp;sensorID))) {
		/// wprintf(L"SensorID: %s\n", GUIDToString(sensorID, szGUID, ARRAYSIZE(szGUID))); }</c>
		/// </para>
		/// </remarks>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilocationreport-getsensorid HRESULT GetSensorID(
		// [out] SENSOR_ID *pSensorID );
		Guid GetSensorID();

		/// <summary>Retrieves the date and time when the report was generated.</summary>
		/// <returns>
		/// Address of a <b>SYSTEMTIME</b> that receives the date and time when the report was generated. Time stamps are provided as
		/// Coordinated Universal Time (UTC).
		/// </returns>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilocationreport-gettimestamp HRESULT GetTimestamp(
		// [out] SYSTEMTIME *pCreationTime );
		SYSTEMTIME GetTimestamp();

		/// <summary>Retrieves a property value from the location report.</summary>
		/// <param name="pKey"><b>REFPROPERTYKEY</b> that specifies the name of the property to retrieve.</param>
		/// <returns>Address of a <b>PROPVARIANT</b> that receives the property value.</returns>
		/// <remarks>
		/// <para>The properties may be platform defined or manufacturer defined.</para>
		/// <para>
		/// Platform-defined <b>PROPERTYKEY</b> values for location sensors are defined in the location sensor data types section of sensors.h.
		/// </para>
		/// <para>
		/// The following is a table of some platform-defined properties that are commonly association with location reports. These property
		/// keys have a <b>fmtid</b> field equal to <b>SENSOR_DATA_TYPE_LOCATION_GUID</b>. Additional properties can be found in sensors.h.
		/// If you are implementing your own location report to pass to <c>SetReport</c>, this table indicates which values must be supplied
		/// in your report object's implementation of <b>GetValue</b>.
		/// </para>
		/// <list type="table">
		/// <listheader>
		/// <description>Property key name and type</description>
		/// <description>Description</description>
		/// </listheader>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_LATITUDE <b>VT_R8</b></description>
		/// <description>Degrees latitude where North is positive.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_LONGITUDE <b>VT_R8</b></description>
		/// <description>Degrees longitude where East is positive.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_ALTITUDE_SEALEVEL_METERS <b>VT_R8</b></description>
		/// <description>Altitude with respect to sea level, in meters.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_ALTITUDE_ELLIPSOID_METERS <b>VT_R8</b></description>
		/// <description>Altitude with respect to the reference ellipsoid, in meters.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_SPEED_KNOTS <b>VT_R8</b></description>
		/// <description>Speed measured in knots.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_TRUE_HEADING_DEGREES <b>VT_R8</b></description>
		/// <description>Heading relative to true north in degrees.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_MAGNETIC_HEADING_DEGREES <b>VT_R8</b></description>
		/// <description>Heading relative to magnetic north in degrees.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_MAGNETIC_VARIATION <b>VT_R8</b></description>
		/// <description>Magnetic variation. East is positive.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_ERROR_RADIUS_METERS <b>VT_R8</b></description>
		/// <description>Error radius that indicates accuracy of latitude and longitude in meters.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_ADDRESS1 <b>VT_LPWSTR</b></description>
		/// <description>First line of the address in a civic address report.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_ADDRESS2 <b>VT_LPWSTR</b></description>
		/// <description>Second line of the address in a civic address report.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_CITY <b>VT_LPWSTR</b></description>
		/// <description>City field in a civic address report.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_STATE_PROVINCE <b>VT_LPWSTR</b></description>
		/// <description>State/province field in a civic address report.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_POSTALCODE <b>VT_LPWSTR</b></description>
		/// <description>Postal code field in a civic address report.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_COUNTRY_REGION <b>VT_LPWSTR</b></description>
		/// <description>
		/// Country/region code in a civic address report. The value must be a two-letter or three-letter ISO 3166 country code.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_ALTITUDE_ELLIPSOID_ERROR_METERS <b>VT_R8</b></description>
		/// <description>Altitude error with respect to the reference ellipsoid, in meters.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_DATA_TYPE_ALTITUDE_SEALEVEL_ERROR_METERS <b>VT_R8</b></description>
		/// <description>Altitude error with respect to sea level, in meters.</description>
		/// </item>
		/// </list>
		/// <para></para>
		/// <para>
		/// The following is a table of other platform-defined properties that may occur in location reports but are not specific to
		/// location. The <b>fmtid</b> field of these property keys is <b>SENSOR_PROPERTY_COMMON_GUID</b>.
		/// </para>
		/// <list type="table">
		/// <listheader>
		/// <description>Property key name and type</description>
		/// <description>Description</description>
		/// </listheader>
		/// <item>
		/// <description>SENSOR_PROPERTY_ACCURACY <b>VT_UNKNOWN</b></description>
		/// <description>
		/// IPortableDeviceValues object that contains sensor data type names and their associated accuracies. Accuracy values represent
		/// possible variation from true values. Accuracy values are expressed by using the same units as the data field, except when
		/// otherwise documented.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_CHANGE_SENSITIVITY <b>VT_UNKNOWN</b></description>
		/// <description>
		/// <b>IPortableDeviceValues</b> object that contains sensor data type names and their associated change sensitivity values. Change
		/// sensitivity values represent the amount by which the data field must change before the SENSOR_EVENT_DATA_UPDATED event is raised.
		/// Sensitivity values are expressed by using the same units as the data field, except where otherwise documented. For example, a
		/// change sensitivity value of 2 for SENSOR_DATA_TYPE_TEMPERATURE_CELSIUS would represent a sensitivity of plus or minus 2 degrees Celsius.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_CURRENT_CONNECTION_TYPE <b>VT_UI4</b></description>
		/// <description><c>SensorConnectionType</c> value that contains the current connection type.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_CURRENT_REPORT_INTERVAL <b>VT_UI4</b></description>
		/// <description>
		/// The current elapsed time for sensor data report generation, in milliseconds. Setting a value of zero signals the driver to use
		/// its default report interval. After receiving a value of zero for this property, a driver must return its default report interval,
		/// not zero, when queried. Applications can set this value to request a particular report interval, but multiple applications might
		/// be using the same driver. Therefore, drivers determine the true report interval that is based on internal logic. For example, the
		/// driver might always use the shortest report interval that is requested by the caller.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_DESCRIPTION <b>VT_LPWSTR</b></description>
		/// <description>The sensor description string.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_DEVICE_PATH <b>VT_LPWSTR</b></description>
		/// <description>
		/// Uniquely identifies the device instance with which the sensor is associated. You can use this property to determine whether a
		/// device contains multiple sensors. Device drivers do not have to support this property because the platform provides this value to
		/// applications without querying drivers.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_FRIENDLY_NAME <b>VT_LPWSTR</b></description>
		/// <description>The friendly name for the device.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_LOCATION_DESIRED_ACCURACY <b>VT_UI4</b></description>
		/// <description>
		/// An enumerated value that indicates the type of accuracy handling requested by a client application.
		/// <b>LOCATION_DESIRED_ACCURACY_DEFAULT</b> (0) indicates that the sensor should use the accuracy for which it can optimize power
		/// use and other cost considerations. <b>LOCATION_DESIRED_ACCURACY_HIGH</b> (1) indicates that the sensor should deliver the most
		/// accurate report possible. This includes using services that might charge money, or consuming higher levels of battery power or
		/// connection bandwidth.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_MANUFACTURER <b>VT_LPWSTR</b></description>
		/// <description>The manufacturer's name.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_MIN_REPORT_INTERVAL <b>VT_UI4</b></description>
		/// <description>The minimum elapsed time setting that the hardware supports for sensor data report generation, in milliseconds.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_MODEL <b>VT_LPWSTR</b></description>
		/// <description>The sensor model name.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_PERSISTENT_UNIQUE_ID <b>VT_CLSID</b></description>
		/// <description>
		/// A <b>GUID</b> that identifies the sensor. This value must be unique for each sensor on a device, or across devices of the same
		/// model as enumerated on the computer.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_RANGE_MAXIMUM <b>VT_UKNOWN</b></description>
		/// <description><b>IPortableDeviceValues</b> object that contains sensor data field names and their associated maximum values.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_RANGE_MINIMUM <b>VT_UKNOWN</b></description>
		/// <description><b>IPortableDeviceValues</b> object that contains sensor data field names and their associated minimum values.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_RESOLUTION <b>VT_UKNOWN</b></description>
		/// <description>
		/// <b>IPortableDeviceValues</b> object that contains sensor data field names and their associated resolutions. Resolution values
		/// represent sensitivity to change in the data field. Resolution values are expressed by using the same units as the data field,
		/// except when otherwise documented.
		/// </description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_SERIAL_NUMBER <b>VT_LPWSTR</b></description>
		/// <description>The sensor serial number.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_STATE <b>VT_UI4</b></description>
		/// <description><c>SensorState</c> value that contains the current sensor state.</description>
		/// </item>
		/// <item>
		/// <description>SENSOR_PROPERTY_TYPE <b>VT_CLSID</b></description>
		/// <description>A <b>GUID</b> that identifies the sensor type. Platform-defined sensor types are defined in Sensors.h.</description>
		/// </item>
		/// </list>
		/// <para></para>
		/// <para>Examples</para>
		/// <para>
		/// The following example demonstrates how to call <b>GetValue</b> to get a property value. You must include sensors.h to use the
		/// constant in the example.
		/// </para>
		/// <para><c>PROPVARIANT pv; HRESULT hr = spLatLongReport-&gt;GetValue(SENSOR_DATA_TYPE_LATITUDE_DEGREES, &amp;pv);</c></para>
		/// <para>
		/// The following example shows how to implement <b>GetValue</b> in your own report object. This implementation allows the caller to
		/// get values for several location report fields. This code requires you to include sensors.h and provarutil.h.
		/// </para>
		/// <para>
		/// <c>STDMETHODIMP CLocationReport::GetValue(REFPROPERTYKEY pKey, PROPVARIANT *pValue) { HRESULT hr = S_OK; if (pKey.fmtid ==
		/// SENSOR_DATA_TYPE_LOCATION_GUID) { // properties for civic address reports if (pKey.pid == SENSOR_DATA_TYPE_ADDRESS1.pid) { hr =
		/// InitPropVariantFromString(m_address1, pValue); } else if (pKey.pid == SENSOR_DATA_TYPE_ADDRESS2.pid) { hr =
		/// InitPropVariantFromString(m_address2, pValue); } else if (pKey.pid == SENSOR_DATA_TYPE_CITY.pid) { hr =
		/// InitPropVariantFromString(m_city, pValue); } else if (pKey.pid == SENSOR_DATA_TYPE_STATE_PROVINCE.pid) { hr =
		/// InitPropVariantFromString(m_stateprovince, pValue); } else if (pKey.pid == SENSOR_DATA_TYPE_POSTALCODE.pid) { hr =
		/// InitPropVariantFromString(m_postalcode, pValue); } else if (pKey.pid == SENSOR_DATA_TYPE_COUNTRY_REGION.pid) { hr =
		/// InitPropVariantFromString(m_countryregion, pValue); } // properties for latitude/longitude reports else if (pKey.pid ==
		/// SENSOR_DATA_TYPE_LATITUDE_DEGREES.pid) { hr = InitPropVariantFromDouble(m_latitude, pValue); } else if (pKey.pid ==
		/// SENSOR_DATA_TYPE_LONGITUDE_DEGREES.pid) { hr = InitPropVariantFromDouble(m_longitude, pValue); } else if (pKey.pid ==
		/// SENSOR_DATA_TYPE_ERROR_RADIUS_METERS.pid) { hr = InitPropVariantFromDouble(m_errorradius, pValue); } else { hr =
		/// HRESULT_FROM_WIN32(ERROR_NO_DATA); PropVariantInit(pValue); } } return hr; }</c>
		/// </para>
		/// </remarks>
		// https://learn.microsoft.com/en-us/windows/win32/api/locationapi/nf-locationapi-ilocationreport-getvalue HRESULT GetValue( [in]
		// REFPROPERTYKEY pKey, [out] PROPVARIANT *pValue );
		PROPVARIANT GetValue(in PROPERTYKEY pKey);
	}

	/// <summary/>
	[ComImport, Guid("2DAEC322-90B2-47e4-BB08-0DA841935A6B"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
	public interface ILocationReportFactory
	{
		/// <summary/>
		[PreserveSig]
		HRESULT ListenForReports(uint requestedReportInterval = 0);

		/// <summary/>
		[PreserveSig]
		HRESULT StopListeningForReports();

		/// <summary/>
		uint Status { get; }

		/// <summary/>
		uint ReportInterval { get; set; }

		/// <summary/>
		uint DesiredAccuracy { get; set; }

		/// <summary/>
		[PreserveSig]
		HRESULT RequestPermissions(in uint hWnd);
	}

	[ComImport, Guid("D39E7BDD-7D05-46b8-8721-80CF035F57D7"), ClassInterface(ClassInterfaceType.None)]
	public class CivicAddressReport
	{ }

	[ComImport, Guid("2A11F42C-3E81-4ad4-9CBE-45579D89671A"), ClassInterface(ClassInterfaceType.None)]
	public class CivicAddressReportFactory
	{ }

	[ComImport, Guid("8B7FBFE0-5CD7-494a-AF8C-283A65707506"), ClassInterface(ClassInterfaceType.None)]
	public class DefaultLocation
	{ }

	[ComImport, Guid("4C596AEC-8544-4082-BA9F-EB0A7D8E65C6"), ClassInterface(ClassInterfaceType.None)]
	public class DispCivicAddressReport
	{ }

	[ComImport, Guid("7A7C3277-8F84-4636-95B2-EBB5507FF77E"), ClassInterface(ClassInterfaceType.None)]
	public class DispLatLongReport
	{ }

	[ComImport, Guid("ED81C073-1F84-4ca8-A161-183C776BC651"), ClassInterface(ClassInterfaceType.None)]
	public class LatLongReport
	{ }

	[ComImport, Guid("9DCC3CC8-8609-4863-BAD4-03601F4C65E8"), ClassInterface(ClassInterfaceType.None)]
	public class LatLongReportFactory
	{ }

	[ComImport, Guid("E5B8E079-EE6D-4E33-A438-C87F2E959254"), ClassInterface(ClassInterfaceType.None)]
	public class Location
	{ }
}