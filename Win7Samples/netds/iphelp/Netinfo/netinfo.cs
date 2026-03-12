using Vanara.Extensions;
using static Vanara.PInvoke.IpHlpApi;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.Ws2_32;

NetInfo.AddressDemo();
NetInfo.IpInterfaceDemo();

internal static class NetInfo
{
	public static void AddressDemo()
	{
		// Retrieve all the IP addresses.

		var Status = GetUnicastIpAddressTable(ADDRESS_FAMILY.AF_UNSPEC, out var UnicastTable);
		if (Status.Failed)
		{
			Console.Write("GetUnicastAddressTable Failed. Error {0}\n", Status);
		}
		else
		{
			Console.Write("GetUnicastAddressTable Succeeded.\n");
			Console.Write("Total Number of all IP Address Entries: {0}.\n",
			UnicastTable.NumEntries);
			for (int i = 0; i < UnicastTable.NumEntries; i++)
			{
				Console.Write("Address {0}: ", i);
				PrintIpAddress(UnicastTable.Table![i].Address);
				Console.Write("\n");
			}
			Console.Write("\n\n");
		}

		// Retrieve IPv6 Only Addresses.

		Status = GetUnicastIpAddressTable(ADDRESS_FAMILY.AF_INET6, out UnicastTable);
		uint InterfaceIndex = 0;
		if (Status.Failed)
		{
			Console.Write("GetUnicastAddressTable Failed. Error {0}\n",
			Status);
		}
		else
		{
			Console.Write("GetUnicastAddressTable Succeeded.\n");
			Console.Write("Total Number of IPv6 Address Entries: {0}.\n",
			UnicastTable.NumEntries);
			int i;
			for (i = 0; i < UnicastTable.NumEntries; i++)
			{
				Console.Write("Address {0}: ", i);
				PrintIpAddress(UnicastTable.Table![i].Address);
				Console.Write("\n");
			}
			InterfaceIndex = UnicastTable.Table![i - 1].InterfaceIndex;
		}

		Status = NotifyUnicastIpAddressChange(ADDRESS_FAMILY.AF_UNSPEC,
			//(PUNICAST_IPADDRESS_CHANGE_CALLBACK)AddressCallbackDemo,
			AddressCallbackDemo,
			default,
			false,
			out var Handle);

		if (Status.Failed)
		{
			Console.Write("\nRegister address change failed. Error {0}\n",
			Status);
		}
		else
		{
			Console.Write("\nRegister address change succeeded.\n");
		}
		IN6_ADDR Ipv6Address = new([0xfe, 0x3f, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x20, 0x00]);
		InitializeUnicastIpAddressEntry(out var Row);
		Row.Address.si_family = ADDRESS_FAMILY.AF_INET6;
		Row.Address.Ipv6 = Ipv6Address;
		Row.InterfaceIndex = InterfaceIndex;
		Status = CreateUnicastIpAddressEntry(ref Row);
		if (Status.Failed)
		{
			Console.Write("Create IPv6 unicast address entry failed. Error {0}\n",
			Status);
		}
		else
		{
			Console.Write("Create IPv6 unicast address entry succeeded.\n");
		}

		Status = GetUnicastIpAddressEntry(ref Row);
		if (Status.Failed)
		{
			Console.Write("Get IPv6 unicast address failed. Error {0}\n", Status);
		}
		else
		{
			Console.Write("Get IPv6 unicast address entry succeeded.\n");
		}

		Row.PreferredLifetime = 500000;
		Status = SetUnicastIpAddressEntry(Row);
		if (Status.Failed)
		{
			Console.Write("Set IPv6 unicast address entry failed. Error {0}\n",
			Status);
		}
		else
		{
			Console.Write("Set IPv6 unicast address entry succeeded.\n");
		}

		Status = DeleteUnicastIpAddressEntry(ref Row);
		if (Status.Failed)
		{
			Console.Write("Delete Ipv6 Unicast Address Failed. Error {0}\n",
			Status);
		}
		else
		{
			Console.Write("Delete Ipv6 Unicast Address Succeeded.\n");
		}

		Sleep(2000);
		Status = CancelMibChangeNotify2(Handle);
		if (Status.Failed)
		{
			Console.Write("Deregister address change failed. Error {0}\n\n",
			Status);
		}
		else
		{
			Console.Write("Deregister address change succeeded.\n\n");
		}
	}

	public static void IpInterfaceDemo()
	{
		var Status = GetIpInterfaceTable(ADDRESS_FAMILY.AF_UNSPEC, out var InterfaceTable);
		if (Status.Failed)
		{
			Console.Write("Get IpInterfaceTable failed. Error {0}\n", Status);
		}
		else
		{
			Console.Write("Get IpInterfaceTable succeeded.\n");
			var InterfaceCount = InterfaceTable.NumEntries;
			Console.Write("Total number of Interfaces: {0}\n",
			InterfaceTable.NumEntries);
			for (int i = 0; i < InterfaceCount; i++)
			{
				Console.Write("\n");
				Console.Write("Interface Index: {0}\n",
				InterfaceTable.Table![i].InterfaceIndex);
				Console.Write("Address Family: {0}\n",
				(InterfaceTable.Table[i].Family == ADDRESS_FAMILY.AF_INET) ?
				"IPv4" : "IPv6");
				Console.Write("AdvertisingEnabled: {0}\n",
				InterfaceTable.Table[i].AdvertisingEnabled ?
				"true" : "false");
				Console.Write("UseAutomaticMetric: {0}\n",
				InterfaceTable.Table[i].UseAutomaticMetric ?
				"true" : "false");
				Console.Write("ForwardingEnabled: {0}\n",
				InterfaceTable.Table[i].ForwardingEnabled ?
				"true" : "false");
				Console.Write("WeakHostSend: {0}\n",
				InterfaceTable.Table[i].WeakHostSend ?
				"true" : "false");
				Console.Write("WeakHostReceive: {0}\n",
				InterfaceTable.Table[i].WeakHostReceive ?
				"true" : "false");
				Console.Write("UseNeighborUnreachabilityDetection: {0}\n",
				InterfaceTable.Table[i].UseNeighborUnreachabilityDetection ?
				"true" : "false");
				Console.Write("AdvertiseDefaultRoute: {0}\n",
				InterfaceTable.Table[i].AdvertiseDefaultRoute ?
				"true" : "false");
			}
		}
	}

	private static void AddressCallbackDemo([In] IntPtr CallerContext, [In, Optional] IntPtr Address, [In] MIB_NOTIFICATION_TYPE NotificationType)
	{
		Console.Write("Address Change Notification Received.\n");
		Console.Write("Notification Type is {0}.\n", NotificationType);

		if (Address != IntPtr.Zero)
		{
			ref var addr = ref Address.AsRef<MIB_UNICASTIPADDRESS_ROW>();
			Console.Write("Interface Index: {0}.\n", addr.InterfaceIndex);
			Console.Write("Address: ");
			PrintIpAddress(addr.Address);
			Console.Write("\n");
		}
	}

	private static void InterfaceCallbackDemo([In] IntPtr CallerContext, [In, Optional] IntPtr InterfaceRow, [In] MIB_NOTIFICATION_TYPE NotificationType)
	{
		Console.Write("Interface Change Notification Received.\n");
		Console.Write("Notification Type is {0}.\n", NotificationType);

		if (InterfaceRow != IntPtr.Zero)
		{
			ref var row = ref InterfaceRow.AsRef<MIB_IPINTERFACE_ROW>();
			Console.Write("Interface Index: {0}.\n", row.InterfaceIndex);
		}
	}

	/*++

	Routine Description:

	Print an IP address.

	Arguments:

	IpAddress - Supplies IP address to be converted.

	AddressString - Returns the string.

	BufferLength - Supplies and returns the string length.

	Returns:

	NONE.

	--*/
	private static void PrintIpAddress(in SOCKADDR_INET IpAddress) => Console.Write(IpAddress);
}