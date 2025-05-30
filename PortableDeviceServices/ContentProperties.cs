using Vanara.PInvoke;
using static Vanara.PInvoke.Ole32;
using static Vanara.PInvoke.PortableDeviceApi;

internal partial class Program
{
	// Displays a property assumed to be in error code form.
	static void DisplayErrorResultProperty([In] IPortableDeviceValues properties, in PROPERTYKEY key, string keyName)
	{
		HRESULT error = properties.GetErrorValue(key);
		Console.Write("{0}: HRESULT = {1})\n", keyName, error);
	}

	// Displays a property assumed to be in string form.
	static void DisplayStringProperty([In] IPortableDeviceValues properties, in PROPERTYKEY key, string keyName)
	{
		string value = properties.GetStringValue(key);

		// Get the length of the string value so we
		// can output <empty string value> if one
		// is encountered.
		if (value.Length > 0)
		{
			Console.Write("{0}: {1}\n", keyName, value);
		}
		else
		{
			Console.Write("{0}: <empty string value>\n", keyName);
		}
	}

	// Displays a property assumed to be in Guid form.
	static void DisplayGuidProperty([In] IPortableDeviceValues properties, in PROPERTYKEY key, string keyName)
	{
		Guid value = properties.GetGuidValue(key);
		Console.Write("{0}: {1}\n", keyName, value);
	}

	// Reads properties for the user specified object.
	static void ReadContentProperties([In] IPortableDeviceService service)
	{
		// Prompt user to enter an object identifier on the device to read properties from.
		Console.Write("Enter the identifier of the object you wish to read properties from.\n>");
		var selection = Console.ReadLine();
		if (selection is null)
		{
			Console.WriteLine("An invalid object identifier was specified, aborting property reading");
			return;
		}

		// 1) Get an IPortableDeviceContent2 interface from the IPortableDeviceService interface to
		// access the content-specific methods.
		IPortableDeviceContent2 content = service.Content();

		// 2) Get an IPortableDeviceProperties interface from the IPortableDeviceContent2 interface
		// to access the property-specific methods.
		IPortableDeviceProperties properties = content.Properties();

		// 3) CoCreate an IPortableDeviceKeyCollection interface to hold the the property keys
		// we wish to read.
		IPortableDeviceKeyCollection propertiesToRead = new();

		// 4) Populate the IPortableDeviceKeyCollection with the keys we wish to read.
		// NOTE: We are not handling any special error cases here so we can proceed with
		// adding as many of the target properties as we can.
		propertiesToRead.Add(PKEY_GenericObj_ParentID);

		propertiesToRead.Add(PKEY_GenericObj_Name);

		propertiesToRead.Add(PKEY_GenericObj_PersistentUID);

		propertiesToRead.Add(PKEY_GenericObj_ObjectFormat);

		// 5) Call GetValues() passing the collection of specified PROPERTYKEYs.
		IPortableDeviceValues objectProperties = properties.GetValues(selection, // The object whose properties we are reading
			propertiesToRead); // The properties we want to read

		// 6) Display the returned property values to the user
		DisplayStringProperty(objectProperties, PKEY_GenericObj_ParentID, NAME_GenericObj_ParentID);
		DisplayStringProperty(objectProperties, PKEY_GenericObj_Name, NAME_GenericObj_Name);
		DisplayStringProperty(objectProperties, PKEY_GenericObj_PersistentUID, NAME_GenericObj_PersistentUID);
		DisplayGuidProperty(objectProperties, PKEY_GenericObj_ObjectFormat, NAME_GenericObj_ObjectFormat);
	}

	// Writes properties on the user specified object.
	static void WriteContentProperties([In] IPortableDeviceService service)
	{
		// Prompt user to enter an object identifier on the device to write properties on.
		Console.Write("Enter the identifier of the object you wish to write properties on.\n>");
		var selection = Console.ReadLine();
		if (selection is null)
		{
			Console.Write("An invalid object identifier was specified, aborting property reading\n");
			return;
		}

		// 1) Get an IPortableDeviceContent2 interface from the IPortableDeviceService interface to
		// access the content-specific methods.
		IPortableDeviceContent2 content = service.Content();

		// 2) Get an IPortableDeviceProperties interface from the IPortableDeviceContent2 interface
		// to access the property-specific methods.
		IPortableDeviceProperties properties = content.Properties();

		// 3) Check the property attributes to see if we can write/change the NAME_GenericObj_Name property.
		IPortableDeviceValues attributes = properties.GetPropertyAttributes(selection, PKEY_GenericObj_Name);

		bool canWrite = attributes.GetBoolValue(WPD_PROPERTY_ATTRIBUTE_CAN_WRITE);
		if (canWrite)
		{
			Console.Write("The attribute WPD_PROPERTY_ATTRIBUTE_CAN_WRITE for PKEY_GenericObj_Name reports true\nThis means that the property can be changed/updated\n\n");
		}
		else
		{
			Console.Write("The attribute WPD_PROPERTY_ATTRIBUTE_CAN_WRITE for PKEY_GenericObj_Name reports false\nThis means that the property cannot be changed/updated\n\n");
		}

		// 4) Prompt the user for the new value of the NAME_GenericObj_Name property only if the property attributes report
		// that it can be changed/updated.
		if (canWrite)
		{
			Console.Write("Enter the new PKEY_GenericObj_Name property for the object '{0}'.\n>", selection);
			var newObjectName = Console.ReadLine();
			if (newObjectName is null)
			{
				Console.Write("An invalid PKEY_GenericObj_Name was specified, aborting property writing\n");
				return;
			}

			// 5) CoCreate an IPortableDeviceValues interface to hold the the property values
			// we wish to write.
			IPortableDeviceValues objectPropertiesToWrite = new();
			objectPropertiesToWrite.SetStringValue(PKEY_GenericObj_Name, newObjectName);

			// 6) Call SetValues() passing the collection of specified PROPERTYKEYs.
			IPortableDeviceValues propertyWriteResults = properties.SetValues(selection, // The object whose properties we are reading
				objectPropertiesToWrite); // The properties we want to read

			Console.Write("The PKEY_GenericObj_Name property on object '{0}' was written successfully (Read the properties again to see the updated value)\n", selection);
		}
	}
}