using static Vanara.PInvoke.Ole32;
using static Vanara.PInvoke.PortableDeviceApi;

internal partial class Program
{
	static void DisplayVarType(VARTYPE vartype) => Console.Write(vartype);

	static string DisplayParameterForm(WpdParameterAttributeForm form) => form switch
	{
		WpdParameterAttributeForm.WPD_PARAMETER_ATTRIBUTE_FORM_RANGE => "Range",
		WpdParameterAttributeForm.WPD_PARAMETER_ATTRIBUTE_FORM_ENUMERATION => "Enumeration",
		WpdParameterAttributeForm.WPD_PARAMETER_ATTRIBUTE_FORM_REGULAR_EXPRESSION => "Regular Expression",
		WpdParameterAttributeForm.WPD_PARAMETER_ATTRIBUTE_FORM_OBJECT_IDENTIFIER => "Object Identifier",
		_ => "Unspecified",
	};

	// Display the basic event options.
	static void DisplayEventOptions([In] IPortableDeviceValues eventOptions)
	{
		Console.Write("\tEvent Options:\n");
		// Read the WPD_EVENT_OPTION_IS_BROADCAST_EVENT value to see if the event is
		// a broadcast event. If the read fails, assume false
		bool isBroadcastEvent = eventOptions.GetBoolValue(WPD_EVENT_OPTION_IS_BROADCAST_EVENT);
		Console.Write("\tWPD_EVENT_OPTION_IS_BROADCAST_EVENT = {0}\n", isBroadcastEvent);
	}

	// Display the event parameters.
	static void DisplayEventParameters([In] IPortableDeviceServiceCapabilities capabilities, in Guid @event, [In] IPortableDeviceKeyCollection parameters)
	{
		// Get the number of parameters for this event.
		uint numParameters = parameters.GetCount();
		Console.Write("\n\t{0} Event Parameters:\n", numParameters);

		// Loop through each parameter and display it
		for (uint index = 0; index < numParameters; index++)
		{
			PROPERTYKEY parameter = parameters.GetAt(index);

			// Display the parameter's Vartype and Form
			IPortableDeviceValues attributes = capabilities.GetEventParameterAttributes(@event, parameter);
			Console.Write("\t\tPROPERTYKEY: {0}.{1}\n", parameter.fmtid, parameter.pid);

			// Read the WPD_PARAMETER_ATTRIBUTE_VARTYPE value.
			// If the read fails, we don't display it
			uint attributeVarType = attributes.GetUnsignedIntegerValue(WPD_PARAMETER_ATTRIBUTE_VARTYPE);
			Console.Write($"\t\tVARTYPE: {attributeVarType}\n");

			// Read the WPD_PARAMETER_ATTRIBUTE_FORM value.
			// If the read fails, we don't display it
			WpdParameterAttributeForm attributeForm = (WpdParameterAttributeForm)attributes.GetUnsignedIntegerValue(WPD_PARAMETER_ATTRIBUTE_FORM);
			Console.Write($"\t\tForm: {DisplayParameterForm(attributeForm)}\n");
		}

		Console.WriteLine();
	}

	// Display basic information about an event
	static void DisplayEvent([In] IPortableDeviceServiceCapabilities capabilities, in Guid @event)
	{
		// Get the event attributes which describe the event
		IPortableDeviceValues attributes = capabilities.GetEventAttributes(@event);

		// Display the name of the event if it is available. Otherwise, fall back to displaying the Guid.
		try
		{
			string formatName = attributes.GetStringValue(WPD_EVENT_ATTRIBUTE_NAME);
			Console.Write("{0}\n", formatName);
		}
		catch (Exception)
		{
			Console.Write("{0}\n", @event);
		}

		// Display the event options, if any
		IPortableDeviceValues options = attributes.GetIPortableDeviceValuesValue(WPD_EVENT_ATTRIBUTE_OPTIONS);
		DisplayEventOptions(options);

		// Display the event parameters, if any
		IPortableDeviceKeyCollection parameters = attributes.GetIPortableDeviceKeyCollectionValue(WPD_EVENT_ATTRIBUTE_PARAMETERS);
		DisplayEventParameters(capabilities, @event, parameters);
	}

	// Display basic information about a format
	static void DisplayFormat([In] IPortableDeviceServiceCapabilities capabilities, in Guid format)
	{
		IPortableDeviceValues attributes = capabilities.GetFormatAttributes(format);
		try
		{
			string formatName = attributes.GetStringValue(WPD_FORMAT_ATTRIBUTE_NAME);
			// Display the name of the format if it is available, otherwise fall back to displaying the Guid.
			Console.Write(formatName);
		}
		catch (Exception)
		{
			Console.Write(format);
		}
	}

	// List all supported formats on the service
	static void ListSupportedFormats([In] IPortableDeviceService service)
	{
		// Get an IPortableDeviceServiceCapabilities interface from the IPortableDeviceService interface to
		// access the service capabilities-specific methods.
		IPortableDeviceServiceCapabilities capabilities = service.Capabilities();

		// Get all formats supported by the service.
		IPortableDevicePropVariantCollection formats = capabilities.GetSupportedFormats();

		// Get the number of supported formats found on the service.
		uint numFormats = formats.GetCount();

		// Loop through each format and display it
		Console.Write("\n{0} Supported Formats Found on the service\n\n", numFormats);

		for (uint index = 0; index < numFormats; index++)
		{
			using PROPVARIANT format = new();
			formats.GetAt(index, format);

			// We have a format. It is assumed that
			// formats are returned as VT_CLSID VarTypes.
			if (format.vt == VARTYPE.VT_CLSID && format.puuid.HasValue)
			{
				DisplayFormat(capabilities, format.puuid.Value);
				Console.WriteLine();
			}
		}
	}

	// List all supported events on the service
	static void ListSupportedEvents([In] IPortableDeviceService service)
	{
		// Get an IPortableDeviceServiceCapabilities interface from the IPortableDeviceService interface to
		// access the service capabilities-specific methods.
		IPortableDeviceServiceCapabilities capabilities = service.Capabilities();

		// Get all events supported by the service.
		IPortableDevicePropVariantCollection events = capabilities.GetSupportedEvents();

		// Get the number of supported events found on the service.
		uint numEvents = events.GetCount();

		// Loop through each event and display it
		Console.Write("\n{0} Supported Events Found on the service\n\n", numEvents);

		for (uint index = 0; index < numEvents; index++)
		{
			using PROPVARIANT @event = new();
			events.GetAt(index, @event);

			// We have an event. It is assumed that
			// events are returned as VT_CLSID VarTypes.
			if (@event.vt == VARTYPE.VT_CLSID && @event.puuid.HasValue)
			{
				DisplayEvent(capabilities, @event.puuid.Value);
				Console.WriteLine();
			}
		}
	}

	// List the abstract services implemented by the current service.
	// Abstract services represent functionality that a service supports, and are used by applications to discover
	// that a service supports formats, properties and methods associated with that functionality.
	// For example, a Contacts service may implement an abstract service that represents the synchronization models supported 
	// on that device, such as the FullEnumerationSync or AnchorSync Device Services
	static void ListAbstractServices([In] IPortableDeviceService service)
	{
		// Get an IPortableDeviceServiceCapabilities interface from the IPortableDeviceService interface to
		// access the service capabilities-specific methods.
		IPortableDeviceServiceCapabilities capabilities = service.Capabilities();

		// Get all abstract services implemented by the service.
		IPortableDevicePropVariantCollection abstractServices = capabilities.GetInheritedServices(WPD_SERVICE_INHERITANCE_TYPES.WPD_SERVICE_INHERITANCE_IMPLEMENTATION);

		// Get the number of abstract services implemented by the service.
		uint numServices = abstractServices.GetCount();

		// Loop through each abstract service and display it
		Console.Write("\n{0} Abstract Services Implemented by the service\n\n", numServices);

		for (uint index = 0; index < numServices; index++)
		{
			using PROPVARIANT abstractService = new();
			abstractServices.GetAt(index, abstractService);

			if (abstractService.puuid == SERVICE_FullEnumSync)
			{
				Console.Write("\t{0}\n", NAME_FullEnumSyncSvc);
			}
			else if (abstractService.puuid == SERVICE_AnchorSync)
			{
				Console.Write("\t{0}\n", NAME_AnchorSyncSvc);
			}
			else
			{
				Console.Write("\t{0}\n", abstractService.puuid);
			}
		}
	}
}