using Vanara.PInvoke;
using static Vanara.PInvoke.LocationApi;

// Report object that implements ICivicAddressReport and ILatLongReport
internal class CLocationEvents : ILocationEvents // We must include this interface so the Location API knows how to talk to our object
{
	// We store the previous report timestamp here, and use this when computing the time between the current and previous report
	DateTime m_previousTime = default;

	public HRESULT OnLocationChanged(in Guid reportType, [In, Optional] ILocationReport? pLocationReport)
	{
		// If the report type is a Latitude/Longitude report (as opposed to typeof(ICivicAddressReport).Guid or another type)
		if (typeof(ILatLongReport).GUID == reportType)
		{
			try
			{
				ILatLongReport spLatLongReport = pLocationReport as ILatLongReport ?? throw new InvalidCastException();

				// Get the ILatLongReport interface from ILocationReport
				// Print the Report Type Guid
				Console.Write($"\nReportType: {typeof(ILatLongReport).GUID}");

				// Print the Timestamp and the time since the last report
				var currentTime = spLatLongReport.GetTimestamp().ToDateTime(DateTimeKind.Utc);
				Console.Write($"\nTimestamp: {currentTime}\n");

				// Compute the number of 100ns units that difference between the current report's time and the previous report's time.
				var diffTime = (currentTime > m_previousTime) ? (currentTime - m_previousTime) : default;
				m_previousTime = currentTime; // Set the previous time to the current time for the next report.

				// Print the Sensor ID Guid
				Guid sensorID = spLatLongReport.GetSensorID();
				Console.Write($"SensorID: {sensorID}\n");

				// Print the Latitude
				Console.Write($"Latitude: {spLatLongReport.GetLatitude()}\n");

				// Print the Longitude
				Console.Write($"Longitude: {spLatLongReport.GetLongitude()}\n");

				// Print the Altitude
				try { Console.Write($"Altitude: {spLatLongReport.GetAltitude()}\n"); }
				catch 
				{
					// Altitude is optional and may not be available
					Console.Write("Altitude: Not available.\n");
				}

				// Print the Error Radius
				Console.Write($"Error Radius: {spLatLongReport.GetErrorRadius()}\n");

				// Print the Altitude Error
				try { Console.Write($"Altitude Error: {spLatLongReport.GetAltitudeError()}\n"); }
				catch 
				{
					// Altitude Error is optional and may not be available
					Console.Write("Altitude Error: Not available.\n");
				}
			}
			catch (Exception ex)
			{
				Console.Write($"\nError processing ILatLongReport: {ex.Message}\n");
				return ex.HResult;
			}
		}
		return HRESULT.S_OK;
	}

	public HRESULT OnStatusChanged(in Guid reportType, LOCATION_REPORT_STATUS newStatus)
	{
		if (typeof(ILatLongReport).GUID == reportType)
		{
			switch (newStatus)
			{
				case LOCATION_REPORT_STATUS.REPORT_NOT_SUPPORTED:
					Console.Write("\nNo devices detected.\n");
					break;
				case LOCATION_REPORT_STATUS.REPORT_ERROR:
					Console.Write("\nReport error.\n");
					break;
				case LOCATION_REPORT_STATUS.REPORT_ACCESS_DENIED:
					Console.Write("\nAccess denied to reports.\n");
					break;
				case LOCATION_REPORT_STATUS.REPORT_INITIALIZING:
					Console.Write("\nReport is initializing.\n");
					break;
				case LOCATION_REPORT_STATUS.REPORT_RUNNING:
					Console.Write("\nRunning.\n");
					break;
			}
		}
		else if (typeof(ICivicAddressReport).GUID == reportType)
		{
		}

		return HRESULT.S_OK;
	}
}