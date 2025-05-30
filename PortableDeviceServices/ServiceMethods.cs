using Vanara.PInvoke;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.Ole32;
using static Vanara.PInvoke.PortableDeviceApi;

internal partial class Program
{
	static SafeEventHandle g_hMethodCompleteEvent = SafeEventHandle.Null;

	static void DisplayParameterUsage(WPD_PARAMETER_USAGE_TYPES dwUsage) => Console.Write(dwUsage switch
	{
		WPD_PARAMETER_USAGE_TYPES.WPD_PARAMETER_USAGE_RETURN => "Return Value",
		WPD_PARAMETER_USAGE_TYPES.WPD_PARAMETER_USAGE_IN => "Input Parameter",
		WPD_PARAMETER_USAGE_TYPES.WPD_PARAMETER_USAGE_OUT => "Output Parameter",
		WPD_PARAMETER_USAGE_TYPES.WPD_PARAMETER_USAGE_INOUT => "Input/Output Parameter",
		_ => "Unknown Parameter Usage"
	});

	static void DisplayMethodAccess(WPD_COMMAND_ACCESS_TYPES dwAccess) => Console.Write(dwAccess switch
	{
		WPD_COMMAND_ACCESS_TYPES.WPD_COMMAND_ACCESS_READ => "Read",
		WPD_COMMAND_ACCESS_TYPES.WPD_COMMAND_ACCESS_READWRITE => "Read/Write",
		_ => "Unknown Access"
	});

	// Display the method parameters.
	static void DisplayMethodParameters(IPortableDeviceServiceCapabilities pCapabilities, in Guid Method, IPortableDeviceKeyCollection pParameters)
	{
		// Get the number of parameters for this event.
		uint dwNumParameters = pParameters.GetCount();

		Console.Write("\n\t{0} Method Parameters:\n", dwNumParameters);

		// Loop through each parameter and display it
		for (uint dwIndex = 0; dwIndex < dwNumParameters; dwIndex++)
		{
			PROPERTYKEY parameter = pParameters.GetAt(dwIndex);

			// Display the parameter's Name, Usage, Vartype, and Form
			IPortableDeviceValues pAttributes = pCapabilities.GetMethodParameterAttributes(Method, parameter);

			string pszParameterName = pAttributes.GetStringValue(WPD_PARAMETER_ATTRIBUTE_NAME);
			Console.Write("\t\tName: {0}\n", pszParameterName);

			// Read the WPD_PARAMETER_ATTRIBUTE_USAGE value, if specified. 
			var dwAttributeUsage = (WPD_PARAMETER_USAGE_TYPES)pAttributes.GetUnsignedIntegerValue(WPD_PARAMETER_ATTRIBUTE_USAGE);
			Console.Write("\t\tUsage: ");
			DisplayParameterUsage(dwAttributeUsage);
			Console.WriteLine();

			VARTYPE dwAttributeVarType = (VARTYPE)pAttributes.GetUnsignedIntegerValue(WPD_PARAMETER_ATTRIBUTE_VARTYPE);
			Console.Write("\t\tVARTYPE: ");
			DisplayVarType(dwAttributeVarType);
			Console.WriteLine();

			// Read the WPD_PARAMETER_ATTRIBUTE_FORM value.
			var dwAttributeForm = WpdParameterAttributeForm.WPD_PARAMETER_ATTRIBUTE_FORM_UNSPECIFIED;
			try { dwAttributeForm = (WpdParameterAttributeForm)pAttributes.GetUnsignedIntegerValue(WPD_PARAMETER_ATTRIBUTE_FORM); } catch { }
			Console.Write("\t\tForm: ");
			DisplayParameterForm(dwAttributeForm);
			Console.WriteLine();

			Console.WriteLine();
		}
	}


	// Display basic information about a method
	static void DisplayMethod(IPortableDeviceServiceCapabilities pCapabilities, in Guid Method)
	{
		// Get the method attributes which describe the method
		IPortableDeviceValues pAttributes = pCapabilities.GetMethodAttributes(Method);

		// Display the name of the method if available. Otherwise, fall back to displaying the Guid.
		try
		{
			string pszMethodName = pAttributes.GetStringValue(WPD_METHOD_ATTRIBUTE_NAME);
			Console.Write(pszMethodName);
		}
		catch
		{
			Console.Write(Method);
		}

		// Display the method access if available, otherwise default to WPD_COMMAND_ACCESS_READ access
		try
		{
			var dwMethodAccess = (WPD_COMMAND_ACCESS_TYPES)pAttributes.GetUnsignedIntegerValue(WPD_METHOD_ATTRIBUTE_ACCESS);
			Console.Write("\n\tAccess: ");
			DisplayMethodAccess(dwMethodAccess);
		}
		catch { }

		// Display the associated format if specified.
		// Methods that have an associated format may only be supported for that format.
		// Methods that don't have associated formats generally apply to the entire service.
		try
		{
			Guid guidFormat = pAttributes.GetGuidValue(WPD_METHOD_ATTRIBUTE_ASSOCIATED_FORMAT);
			Console.Write("\n\tAssociated Format: ");
			DisplayFormat(pCapabilities, guidFormat);
		}
		catch { }

		// Display the method parameters, if available
		try
		{
			IPortableDeviceKeyCollection pParameters = pAttributes.GetIPortableDeviceKeyCollectionValue(WPD_METHOD_ATTRIBUTE_PARAMETERS);
			DisplayMethodParameters(pCapabilities, Method, pParameters);
		}
		catch { }
	}

	// List all supported methods on the service
	static void ListSupportedMethods(IPortableDeviceService pService)
	{
		// Get an IPortableDeviceServiceCapabilities interface from the IPortableDeviceService interface to
		// access the service capabilities-specific methods.
		IPortableDeviceServiceCapabilities pCapabilities = pService.Capabilities();

		// Get all methods supported by the service.
		IPortableDevicePropVariantCollection pMethods = pCapabilities.GetSupportedMethods();

		// Get the number of supported methods found on the service.
		uint dwNumMethods = pMethods.GetCount();

		Console.Write("\n{0} Supported Methods Found on the service\n\n", dwNumMethods);

		// Loop through each method and display it
		for (uint dwIndex = 0; dwIndex < dwNumMethods; dwIndex++)
		{
			using PROPVARIANT pv = new();
			pMethods.GetAt(dwIndex, pv);

			// We have a method. It is assumed that
			// methods are returned as VT_CLSID VarTypes.
			if (pv.VarType == VarEnum.VT_CLSID)
			{
				DisplayMethod(pCapabilities, pv.puuid.GetValueOrDefault());
				Console.WriteLine();
			}
		}
	}

	class CMethodCallback : IPortableDeviceServiceMethodCallback
	{
		public HRESULT OnComplete(HRESULT hrStatus, IPortableDeviceValues pResults) // We are ignoring results as our methods will not return any results
		{
			Console.Write("** Method completed, status HRESULT = 0x{0:X}\n", (int)hrStatus);
			if (!g_hMethodCompleteEvent.IsInvalid)
			{
				g_hMethodCompleteEvent.Set();
			}
			return HRESULT.S_OK;
		}
	}

	// Invoke methods on the Contacts Service.
	// BeginSync and EndSync are methods defined by the FullEnumerationSync Device Service.
	static void InvokeMethods(IPortableDeviceService pService)
	{
		// Get an IPortableDeviceServiceMethods interface from the IPortableDeviceService interface to
		// invoke methods.
		IPortableDeviceServiceMethods pMethods = pService.Methods();

		// Invoke() the BeginSync method
		// This method does not take any parameters or results, so we pass in default
		try
		{
			pMethods.Invoke(METHOD_FullEnumSyncSvc_BeginSync, default, out _);
			Console.Write("{0} called,\n", NAME_FullEnumSyncSvc_BeginSync);
		}
		catch (Exception ex)
		{
			Console.Write("! Failed to invoke {0}, hr = 0x{1:X}\n", NAME_FullEnumSyncSvc_BeginSync, ex.HResult);
		}

		// Invoke the EndSync method asynchronously
		// This method does not take any parameters or results, so we pass in default
		try
		{
			pMethods.Invoke(METHOD_FullEnumSyncSvc_EndSync, default, out _);
			Console.Write("{0} called,\n", NAME_FullEnumSyncSvc_EndSync);
		}
		catch (Exception ex)
		{
			Console.Write("! Failed to invoke {0}, hr = 0x{1:X}\n", NAME_FullEnumSyncSvc_EndSync, ex.HResult);
		}
	}

	static HRESULT InvokeMethodAsync(IPortableDeviceServiceMethods pMethods, in Guid Method, IPortableDeviceValues? pParameters)
	{
		// Cleanup any previously created global event handles.
		if (!g_hMethodCompleteEvent.IsInvalid)
		{
			g_hMethodCompleteEvent.Dispose();
		}

		// In order to create a simpler to follow example we create and wait infinitely
		// for the method to complete and ignore any errors.
		// Production code should be written in a more robust manner.
		// Create the global event handle to wait on for the method to complete.
		g_hMethodCompleteEvent = CreateEvent();
		if (!g_hMethodCompleteEvent.IsInvalid)
		{
			CMethodCallback pCallback = new();

			try
			{
				// Call InvokeAsync() to begin the Asynchronous method operation. 
				// Each pCallback parameter is used as the method context, and therefore must be unique.
				pMethods.InvokeAsync(Method, pParameters, pCallback);
				// In order to create a simpler to follow example we will wait infinitly for the operation
				// to complete and ignore any errors. Production code should be written in a more
				// robust manner.
				if (!g_hMethodCompleteEvent.IsInvalid)
				{
					g_hMethodCompleteEvent.Wait(INFINITE);
				}
			}
			catch (Exception ex)
			{
				Console.Write("! Failed to invoke method asynchronously, hr = 0x{0:X}\n", ex.HResult);
			}

			// Cleanup any previously created global event handles.
			if (!g_hMethodCompleteEvent.IsInvalid)
			{
				g_hMethodCompleteEvent.Dispose();
			}
		}
		else
		{
			Console.Write("! Failed to create the global event handle to wait on for the method operation. Aborting operation.\n");
			return HRESULT.E_FAIL;
		}

		return HRESULT.S_OK;
	}

	// Invoke methods on the Contacts Service asynchornously.
	// BeginSync and EndSync are methods defined by the FullEnumerationSync Device Service.
	static void InvokeMethodsAsync(IPortableDeviceService pService)
	{
		// Get an IPortableDeviceServiceMethods interface from the IPortableDeviceService interface to
		// invoke methods.
		IPortableDeviceServiceMethods pMethods = pService.Methods();

		// Invoke the BeginSync method asynchronously
		Console.Write("Invoking {0} asynchronously...\n", NAME_FullEnumSyncSvc_BeginSync);

		// This method does not take any parameters, so we pass in default
		InvokeMethodAsync(pMethods, METHOD_FullEnumSyncSvc_BeginSync, null);

		// Invoke the EndSync method asynchronously
		Console.Write("Invoking {0} asynchronously...\n", NAME_FullEnumSyncSvc_EndSync);

		InvokeMethodAsync(pMethods, METHOD_FullEnumSyncSvc_EndSync, null);
	}
}