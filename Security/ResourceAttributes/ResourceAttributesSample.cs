using System.Text.RegularExpressions;
using Vanara.Extensions;
using Vanara.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.AdvApi32;
using static Vanara.PInvoke.Authz;
using static Vanara.PInvoke.Kernel32;

if (args.Length != 4)
{
	PrintUsage();
	return 1;
}

string? FileName = default, CapID = default, Attributes = default;
for (uint i = 0, j = 1; j < args.Length; i += 2, j += 2)
{
	if (HasParam(args[i], "file"))
	{
		FileName = args[j];
	}
	else if (HasParam(args[i], "cap"))
	{
		CapID = args[j];
	}
	else if (HasParam(args[i], "ra"))
	{
		Attributes = args[j];
	}
	else
	{
		PrintUsage();
		return 1;
	}
}

if ((CapID is null) || (Attributes is null && FileName is null) || (Attributes is not null && FileName is not null))
{
	PrintUsage();
	return 1;
}

if (!IsValidCapID((string)CapID, out var CapIDSid))
{
	return 1;
}

IList<CLAIM_SECURITY_ATTRIBUTE_V1> ResourceAttributes = [];
if ((Attributes is not null && !ParseResourceAttributesArguments((string)Attributes, out ResourceAttributes)) ||
	(FileName is not null && !GetFileResourceAttributes(FileName, out ResourceAttributes)))
{
	return 1;
}

if (!CreateSecurityDescriptor(CapIDSid, ResourceAttributes!, out var SecurityDescriptor))
{
	return 1;
}

if (!PerformAccessCheck(SecurityDescriptor))
{
	return 1;
}

return 0;

void PrintUsage() => Console.Write("""
	A command line tool for creating a security descriptor with resource 
	property claim ACEs and a Central Access Policy ACE, and displaying
	the results of AuthzAccessCheck on that security descriptor.

	Usage:

	ResourceAttributesSample [-cap <ID>] [-file <PATH> | -ra <ATTRIBUTES>]
	-cap : A Central Access Policy SID (CAP ID). CAP IDs are of the
	form: S-1-17-...
	-file : A file path used to get resource properties from a file
	to use in the security descriptor. The file's resource
	properties are also printed to the console.
	-ra : Resource properties to add to the security descriptor.
	Resource properties should be specified in the following
	form:
	(Name, ValueType, Flags, Value1, Value2...)
	-Name = Resource Property Name
	-ValueType = TI (Integer)
	= TU (Unsigned Integer)
	= TB (Boolean)
	= TS (String)
	-Flags = Hexadecimal Flag Value
	-Value = Comma separated values
	Examples:

	ResourceAttributesSample -cap S-1-17-11 -file C:\\test.txt
	ResourceAttributesSample -cap S-1-17-22 -ra (\"Int\",TI,0x0,123)
	""");

bool HasParam(string Flag, string syntax) => Flag.Length > 0 && Flag[0] is '-' or '/' && string.Equals(Flag.Substring(1), syntax, StringComparison.InvariantCultureIgnoreCase);

bool IsValidCapID(string CapID, out SafePSID CapIDSidResult)
{
	/*++

	Routine Description:

	Checks if the requested CAP ID is present on the machine. If it is not,
	then the applied CAPs are printed out to the console. If it is present,
	then the string SID is converted to a binary SID and returned to the 
	caller.

	Arguments:

	CapID : The CAP ID requested

	CapIDSidResult : The SID form of the requested CAP ID returned if the
	CAP is present on the machine, otherwise default

	Return Value:

	true/false - If the CAP is present, true is returned, otherwise false.

	--*/
	bool IsValidCapID = false;

	CapIDSidResult = SafePSID.Null;
	if (default == CapID)
	{
		Console.Write("ERROR: Invalid argument\n");
		return false;
	}

	try
	{
		// Convert the CAPID string to a SID
		using SafePSID CapIDSid = ConvertStringSidToSid(CapID);

		// Check that the CAPID is a valid SID
		if (!CapIDSid.IsValidSid)
		{
			Console.Write("ERROR: The supplied CAPID is not a valid SID\n");
			return false;
		}

		// Check that the SID is a valid CAP ID
		if (CapIDSid.Authority != KnownSIDAuthority.SECURITY_SCOPED_POLICY_ID_AUTHORITY)
		{
			Console.Write("ERROR: The CAPID doesn't have the correct authority S-1-17\n");
			return false;
		}

		// Get the applied CAPIDs on the machine
		PSID[] AppliedCaps = [.. LsaGetAppliedCAPIDs()];

		// Search the applied CAPs on the machine for a match to the requested CAP
		IsValidCapID = AppliedCaps.Any(i => i.Equals(CapIDSid));

		if (IsValidCapID)
		{
			CapIDSidResult = CapIDSid;
		}
		else
		{
			// The requested CAPID is not part of the applied Central Policies
			Console.Write("The requested CAPID could not be found on the target machine.\nThe applied CAPIDs are:\n");
			if (AppliedCaps.Length == 0)
				Console.WriteLine(" (empty)");
			else
				Console.WriteLine(string.Join('\n', AppliedCaps.Select(i => " " + i.ToString("D"))));
		}
	}
	catch (Exception ex)
	{
		Console.Write("ERROR: Exception occurred during cap check: {0}\n", ex.Message);
	}
	return IsValidCapID;
}

bool GetFileResourceAttributes(string FileName, out IList<CLAIM_SECURITY_ATTRIBUTE_V1> FileResourceAttributes)
/*++

Routine Description:

Gets the resource attribute ACEs from a file, parses the SDDL for the
resource attributes ACEs to print them in a friendly format to the console.
Then it interprets the resource attributes SDDL and populates a resource
claim structure that can be used to add a resouce attribute ACE to a 
security descriptor.

Arguments:

FileName : The file to get the resource properties from.

FileResourceAttributes : The resulting array of resource attribute 
structures.

Count : The number of claims in the array.

Return Value:

true/false - If the function succeeds, true is returned, otherwise false.

--*/
{
	FileResourceAttributes = [];
	if (default == FileName)
	{
		Console.Write("ERROR: Invalid argument\n");
		return false;
	}

	try
	{
		//
		// Get the security descriptor for the file.
		// Note that we will use the SecurityDescriptor, but Sacl is now
		// also pointing to the attribute ACEs.
		//
		GetNamedSecurityInfo(FileName, SE_OBJECT_TYPE.SE_FILE_OBJECT, SECURITY_INFORMATION.ATTRIBUTE_SECURITY_INFORMATION, out _, out _, out _,
			out var Sacl, out var SecurityDescriptor).ThrowIfFailed();

		// Convert the binary security descriptor to SDDL.
		var SDDL = SecurityDescriptor.ToString(SECURITY_INFORMATION.ATTRIBUTE_SECURITY_INFORMATION);

		// Count the number of resource attributes in the SDDL
		var AttributeCount = Regex.Count(SDDL, @"\(RA;");

		if (AttributeCount < 1)
		{
			Console.Write($"\nThe file {FileName} does not have resource attributes\n");
			return false;
		}

		// Parse the SDDL string to pull out the name, flags, type, and values
		// tokens from each resource attribute.
		ParseResourceAttributesArguments(SDDL, out var ResourceAttributes);

		/*RESOURCE_ATTRIBUTE[] AttributeTokens = Regex.Matches(SDDL, @"\(RA;(?:.*?;){5}.*?\(([^)]*).*?\).*?\)").Select(m => ParseResourceAttributeString(m.Groups[1].Value)).ToArray();

		// Allocate an array of resource claim structures to fill and return
		SafeNativeArray<CLAIM_SECURITY_ATTRIBUTE_V1> ResourceAttributes = new(AttributeCount);

		// Loop over the parsed resource attributes to print the values to the
		// console, and fill the claim structures to return to the caller.
		Console.Write("\nResource attributes for file %ws:\n\n", FileName);
		for (int CurrentAttribute = 0; CurrentAttribute < AttributeCount; CurrentAttribute++)
		{
			PrintResourceAttribute(AttributeTokens[CurrentAttribute]);
			InterpretResourceAttribute(AttributeTokens[CurrentAttribute], out var r);
			ResourceAttributes[CurrentAttribute] = r.Value;
			ResourceAttributes.AddSubReference(r);
		}*/

		FileResourceAttributes = ResourceAttributes;
		return true;
	}
	catch (Exception ex)
	{
		Console.Write("ERROR: Exception occurred during resource attribute fetch: {0}\n", ex.Message);
	}
	return false;
}

bool ParseResourceAttributesArguments(string ResourceAttributesArgs, out IList<CLAIM_SECURITY_ATTRIBUTE_V1> ResourceAttributesResult)
/*++

Routine Description:

Parses the resource attribute arguments passed in on the command line using
the SDDL format for resource attributes:
(Name, ValueType, Flags, Value1, Value2,...)

Arguments:

ResourceAttributesArgs : The resource attributes arguments string.

ResourceAttributesResult : An array of parsed resource attributes in claim
structures that can be used to add a resource
claim to a security descriptor.

Count : The number of claims in the array.

Return Value:

true/false - If the function succeeds, true is returned, otherwise false.

--*/
{
	//bool Succeeded = false;
	//ref PCLAIM_SECURITY_ATTRIBUTE_V1 ResourceAttributes = default;
	//uint AttributeCount;
	//string NextToken = default;
	//string NextAttribute = default;
	//uint ByteCount;
	//RESOURCE_ATTRIBUTE Attribute = default;
	//uint CurrentAttribute;

	ResourceAttributesResult = [];
	if (string.IsNullOrEmpty(ResourceAttributesArgs))
	{
		Console.Write("ERROR: Invalid argument\n");
		return false;
	}

	// Get the number of resource attributes by searching for the "(" token
	// Resource attributes must be of the form: (Name, Type, Flags, Value1,...)
	var matches = Regex.Matches(ResourceAttributesArgs, @"\(\s*(?<name>"".*?"")\s*,\s*(?<type>.*?)\s*,\s*(?<flags>\d+|0x[0-9a-fA-F]+)\s*(?:,(.*?))+\)");
	var AttributeCount = matches.Count;
	if (AttributeCount < 1)
	{
		Console.Write("ERROR: Invalid resource attributes parameter format\n");
		return false;
	}

	// Allocate an array of claim structures
	ResourceAttributesResult = new SafeNativeArray<CLAIM_SECURITY_ATTRIBUTE_V1>(AttributeCount);

	// Iterate over the resource attributes argument, parsing each attribute,
	// then convert the string resource attribute into a claim structure 
	// required for adding the resource claims to an ACE
	Console.Write("\nThe requested resource attributes are:\n\n");
	for (int i = 0; i < AttributeCount; i++)
	{
		var match = matches[i];
		if (match.Success)
		{
			var Flags = uint.TryParse(match.Groups["flags"].Value, out var f) ? (CLAIM_SECURITY_ATTRIBUTE_FLAG)f : 0;
			var Values = match.Groups[1].Captures.Select(c => c.Value.Trim()).ToArray();
			// Create a resource attribute structure from the parsed values
			var Attribute = new RESOURCE_ATTRIBUTE(match.Groups["name"].Value.Trim('"'), Flags, GetValueType(match.Groups["type"].Value), Values);
			PrintResourceAttribute(Attribute);
			// Convert the string resource structure into an actual claim structure
			if (!InterpretResourceAttribute(Attribute, out var ResourceClaim))
				return false;
			ResourceAttributesResult[i] = ResourceClaim.Value;
			((SafeAllocatedMemoryHandle)ResourceAttributesResult).AddSubReference(ResourceClaim);
		}
	}

	return true;
}

bool InterpretResourceAttribute([In] RESOURCE_ATTRIBUTE AttributeTokens, out SafeHGlobalStruct<CLAIM_SECURITY_ATTRIBUTE_V1> ResourceClaimResult)
/*++

Routine Description:

Takes a tokenized resource attribute string structure and produces a
self-relative claim structure that can be used to add a resource attribute
ACE to a security descriptor.

Arguments:

AttributeTokens : The tokenized resource attribute string structure.

ResourceClaimResult : The resulting resource claim structure.

Return Value:

true/false - If the function succeeds, true is returned, otherwise false.

--*/
{
	ResourceClaimResult = SafeHGlobalStruct<CLAIM_SECURITY_ATTRIBUTE_V1>.Null;

	// Interpret the value type string into an actual value type
	var ValueType = AttributeTokens.Type;
	if (ValueType == 0)
	{
		Console.Write("Claim {0} has an unknown type ({1})", AttributeTokens.Name, AttributeTokens.Type);
		return false;
	}

	// Break up the claim values string into individual values
	var Values = AttributeTokens.Values;

	// Must be at lease one value
	if (Values.Length < 1)
	{
		Console.Write("ERROR: No values for the resource claim\n");
		return false;
	}

	// Get the entire size required for the resource attribute structure, so we
	// can allocate a single block of contiguous memory.
	SizeT NameBytes = AttributeTokens.Name.GetByteCount(true, CharSet.Unicode);
	SizeT ByteCount = ALIGN(Marshal.SizeOf<CLAIM_SECURITY_ATTRIBUTE_V1>());
	SizeT offset = ByteCount;
	ByteCount += ALIGN(NameBytes);
	ByteCount += ALIGN(Values.Length * GetValueSize(ValueType));
	if (ValueType == CLAIM_SECURITY_ATTRIBUTE_TYPE.CLAIM_SECURITY_ATTRIBUTE_TYPE_STRING)
	{
		// Additional allocation for each string in the array
		ByteCount += Values.Sum(v => ALIGN(v.GetByteCount(true, CharSet.Unicode)));
	}

	ResourceClaimResult = new(ByteCount);
	ref CLAIM_SECURITY_ATTRIBUTE_V1 ResourceAttribute = ref ResourceClaimResult.AsRef();
	IntPtr NextMemory = ResourceClaimResult.DangerousGetHandle().Offset(offset);

	// Fill out resource claim structure
	ResourceAttribute.ValueType = ValueType;
	ResourceAttribute.ValueCount = (uint)Values.Length;
	ResourceAttribute.Name = NextMemory;

	StringHelper.Write(AttributeTokens.Name, NextMemory, out _, true, CharSet.Unicode, NameBytes);
	NextMemory += ALIGN(NameBytes);

	// Convert the string hex flags to the actual integer value
	CLAIM_SECURITY_ATTRIBUTE_FLAG Flags = AttributeTokens.Flags;
	if (!Flags.IsValid())
	{
		Console.Write("ERROR: Invalid flags\n");
		return false;
	}
	ResourceAttribute.Flags = Flags;

	// Fill up the values array
	switch (ValueType)
	{
		case CLAIM_SECURITY_ATTRIBUTE_TYPE.CLAIM_SECURITY_ATTRIBUTE_TYPE_INT64:
			ResourceAttribute.Values.pInt64 = NextMemory;
			for (int i = 0; i < Values.Length; ++i)
			{
				ResourceAttribute.Values.pInt64[i] = long.TryParse(Values[i], out var l) ? l : 0L;
			}
			break;
		case CLAIM_SECURITY_ATTRIBUTE_TYPE.CLAIM_SECURITY_ATTRIBUTE_TYPE_UINT64:
		case CLAIM_SECURITY_ATTRIBUTE_TYPE.CLAIM_SECURITY_ATTRIBUTE_TYPE_BOOLEAN:
			ResourceAttribute.Values.pUint64 = NextMemory;
			for (int i = 0; i < Values.Length; ++i)
			{
				ResourceAttribute.Values.pUint64[i] = ulong.TryParse(Values[i], out var l) ? l : 0UL;
			}
			break;
		case CLAIM_SECURITY_ATTRIBUTE_TYPE.CLAIM_SECURITY_ATTRIBUTE_TYPE_STRING:
			ResourceAttribute.Values.ppString = NextMemory;
			NextMemory += ALIGN(Values.Length * GetValueSize(ValueType));
			for (int i = 0; i < Values.Length; ++i)
			{
				ResourceAttribute.Values.ppString[i] = NextMemory;
				StringHelper.Write(Values[i], NextMemory, out var l, true, CharSet.Unicode);
				NextMemory += ALIGN(l);
			}
			break;
		default:
			break;
	}

	return true;

	static int ALIGN(SizeT Size) => (int)Macros.ALIGN_TO_MULTIPLE(Size, 4);
}

bool CreateSecurityDescriptor([In] PSID CapIDSid, [In] IList<CLAIM_SECURITY_ATTRIBUTE_V1> ResourceAttributes, out SafePSECURITY_DESCRIPTOR SecurityDescriptorResult)
/*++

Routine Description:

Creates a self-relative security descriptor (SD) with System as the 
owner and group. The DACL of the SD grants everyone full control, and
a SACL is created with a CAPID ACE and resource attribute ACEs and added
to the SD.

Arguments:

CapIDSid : The SID of the CAP ID Ace to add to the SD

ResourceAttributes : An array of resource attributes to add to the SD

ResourceAttributeCt : The length of the ResourceAttributes array

SecurityDescriptorResult : The resulting SD upon success, otherwise default

Return Value:

true/false - If the function succeeds, true is returned, otherwise false.

--*/
{
	bool Succeeded = false;

	SecurityDescriptorResult = SafePSECURITY_DESCRIPTOR.Null;
	if (CapIDSid.IsInvalid || ResourceAttributes is null)
	{
		Console.Write("ERROR: Invalid argument\n");
		return false;
	}

	try
	{
		//
		// Create a default security descriptor with system as the owner, system
		// as a the primary group, and a DACL that grants everyone full control
		//

		// Initialize the security descriptor
		using SafePSECURITY_DESCRIPTOR SecurityDescriptor = new();
		Win32Error.ThrowLastErrorIfFalse(InitializeSecurityDescriptor(SecurityDescriptor, SECURITY_DESCRIPTOR_REVISION));

		// Create the sytem SID for the security descriptor owner/group
		Win32Error.ThrowLastErrorIfFalse(AllocateAndInitializeSid(KnownSIDAuthority.SECURITY_NT_AUTHORITY, 1, KnownSIDRelativeID.SECURITY_LOCAL_RID, 0, 0, 0, 0, 0, 0, 0, out var SystemSID));

		// Create the everyone SID
		Win32Error.ThrowLastErrorIfFalse(AllocateAndInitializeSid(KnownSIDAuthority.SECURITY_WORLD_SID_AUTHORITY, 1, KnownSIDRelativeID.SECURITY_WORLD_RID, 0, 0, 0, 0, 0, 0, 0, out var EveryoneSID));

		// Set the owner of the security descriptor to System.
		Win32Error.ThrowLastErrorIfFalse(SetSecurityDescriptorOwner(SecurityDescriptor, SystemSID, true));

		// Set the primary group to System.
		Win32Error.ThrowLastErrorIfFalse(SetSecurityDescriptorGroup(SecurityDescriptor, SystemSID, true));

		// Initialize the DACL
		using SafePACL Dacl = new(1024);

		// Add an ACE to the DACL to grant everyone full control
		Win32Error.ThrowLastErrorIfFalse(AddAccessAllowedAce(Dacl, ACL_REVISION, (int)REGSAM.KEY_ALL_ACCESS, EveryoneSID));

		// Now add the dACL to the security descriptor.
		Win32Error.ThrowLastErrorIfFalse(SetSecurityDescriptorDacl(SecurityDescriptor, true, Dacl, false));

		// Initialize the SACL.
		// Note: The ACL buffer is a static 1K buffer, so adding a large number of 
		// resource attribute ACEs will cause AddResourceAttributeAce to fail if 
		// the size exceeds the 1K limit. If you have a need for a large number 
		// of ACEs, consider increasing the buffer size or using dynamic memory
		// allocation.
		using SafePACL Sacl = new(1024);

		// Add a CAPID Ace to the ACL 
		Win32Error.ThrowLastErrorIfFalse(AddScopedPolicyIDAce(Sacl, ACL_REVISION, 0, 0, CapIDSid));

		// Build up the claim information structure.
		var ClaimInfo = CLAIM_SECURITY_ATTRIBUTES_INFORMATION.Default;
		ClaimInfo.AttributeCount = 1;

		// Iterate over the resource claims and add an ACE for each one.
		uint Bytes = 0;
		var pResourceAttributes = ResourceAttributes.Select(ra => new SafeHGlobalStruct<CLAIM_SECURITY_ATTRIBUTE_V1>(ra)).ToArray();
		for (uint i = 0; i < ResourceAttributes.Count; ++i)
		{
			ClaimInfo.Attribute.pAttributeV1 = pResourceAttributes[i];
			Win32Error.ThrowLastErrorIfFalse(AddResourceAttributeAce(Sacl, ACL_REVISION, 0, 0, EveryoneSID, ClaimInfo, out Bytes));
		}

		// Now add the SACL to the security descriptor.
		Win32Error.ThrowLastErrorIfFalse(SetSecurityDescriptorSacl(SecurityDescriptor, true, Sacl, false));

		// Convert to a self relative security descriptor
		SecurityDescriptorResult = SecurityDescriptor.MakeSelfRelative();

		// We have successfully created a self-relative security descriptor 
		// with the Central Access Policy and resource attributes
		Succeeded = true;
	}
	catch (Exception ex)
	{
		PrintErrorMessage("ERROR: Exception occurred during security descriptor creation:", ex);
	}

	return Succeeded;
}

bool PerformAccessCheck([In] PSECURITY_DESCRIPTOR SecurityDescriptor)
/*++

Routine Description:

Initializes an Authz context and calls AuthzAccessCheck on the supplied
security descriptor. The results of the access check are printed out to
the console.

Arguments:

SecurityDescriptor : The security descriptor used in the access check.

Return Value:

true/false - If the function succeeds, true is returned, otherwise false.

--*/
{
	if (SecurityDescriptor.IsNull)
	{
		Console.Write("ERROR: Invalid argument\n");
		return false;
	}

	try
	{
		// Open the process token
		using var Token = SafeHTOKEN.FromProcess(GetCurrentProcess(), TokenAccess.TOKEN_QUERY);

		// Initialize the resource manager for AuthzAccessCheck
		Win32Error.ThrowLastErrorIfFalse(AuthzInitializeResourceManager(AuthzResourceManagerFlags.AUTHZ_RM_FLAG_NO_AUDIT, rm: out var AuthzResMgrHandle));

		// Get the authorization context from the token to use for access check
		LUID ZeroLuid = new();
		Win32Error.ThrowLastErrorIfFalse(AuthzInitializeContextFromToken(0, Token, AuthzResMgrHandle, default, ZeroLuid, default, out var AuthzClientHandle));

		// Initialize the request and reply structures passed to AuthzAccessCheck
		AUTHZ_ACCESS_REQUEST AccessRequest = new(ACCESS_MASK.MAXIMUM_ALLOWED);
		using AUTHZ_ACCESS_REPLY AccessReply = new(1);

		// Perform the actual access check
		Win32Error.ThrowLastErrorIfFalse(AuthzAccessCheck(0, AuthzClientHandle, AccessRequest, default, SecurityDescriptor, default, 0, AccessReply, default));

		// Print out the results of the AuthzAccessCheck
		Console.Write($"\nAccess Mask = 0x{AccessReply.GrantedAccessMaskValues[0]:X8}\n\n");

		return true;
	}
	catch (Exception ex)
	{
		Console.Write("ERROR: Exception occurred during access check: {0}\n", ex.Message);
		return false;
	}
}

void PrintResourceAttribute([In] RESOURCE_ATTRIBUTE Attribute)
/*++

Routine Description:

Helper function to prints out a resource attribute in a friendly format.

Arguments:

Attribute : A structure containing the resource attribute to print.

Return Value:

--*/
{
	string? FriendlyValueType = Attribute.Type switch
	{
		CLAIM_SECURITY_ATTRIBUTE_TYPE.CLAIM_SECURITY_ATTRIBUTE_TYPE_INT64 => "Integer",
		CLAIM_SECURITY_ATTRIBUTE_TYPE.CLAIM_SECURITY_ATTRIBUTE_TYPE_UINT64 => "Unsigned Integer",
		CLAIM_SECURITY_ATTRIBUTE_TYPE.CLAIM_SECURITY_ATTRIBUTE_TYPE_STRING => "String",
		CLAIM_SECURITY_ATTRIBUTE_TYPE.CLAIM_SECURITY_ATTRIBUTE_TYPE_BOOLEAN => "Boolean",
		_ => "Unknown"
	};

	Console.WriteLine($"Resource Attribute:\n - Name : {Attribute.Name}\n - Type : {FriendlyValueType} ({Attribute.Type})\n - Flags : {Attribute.Flags}\n - Values : {string.Join(',', Attribute.Values)}\n");
}

void PrintErrorMessage(string ErrorMessage, Exception ex)
{
	string SystemMsg = ex.ToString() ?? "";
	Console.WriteLine($"{ErrorMessage}{(string.IsNullOrEmpty(ErrorMessage) && !string.IsNullOrEmpty(SystemMsg) ? "" : "\r\n")}{SystemMsg}");
}

CLAIM_SECURITY_ATTRIBUTE_TYPE GetValueType(string ClaimType) => ClaimType.ToUpper() switch
{
	SDDL_INT => CLAIM_SECURITY_ATTRIBUTE_TYPE.CLAIM_SECURITY_ATTRIBUTE_TYPE_INT64,
	SDDL_UINT => CLAIM_SECURITY_ATTRIBUTE_TYPE.CLAIM_SECURITY_ATTRIBUTE_TYPE_UINT64,
	SDDL_WSTRING => CLAIM_SECURITY_ATTRIBUTE_TYPE.CLAIM_SECURITY_ATTRIBUTE_TYPE_STRING,
	SDDL_BOOLEAN => CLAIM_SECURITY_ATTRIBUTE_TYPE.CLAIM_SECURITY_ATTRIBUTE_TYPE_BOOLEAN,
	_ => 0
};

uint GetValueSize(CLAIM_SECURITY_ATTRIBUTE_TYPE ClaimType)
{
	var Size = ClaimType switch
	{
		CLAIM_SECURITY_ATTRIBUTE_TYPE.CLAIM_SECURITY_ATTRIBUTE_TYPE_INT64 => Marshal.SizeOf<long>(),
		CLAIM_SECURITY_ATTRIBUTE_TYPE.CLAIM_SECURITY_ATTRIBUTE_TYPE_UINT64 or CLAIM_SECURITY_ATTRIBUTE_TYPE.CLAIM_SECURITY_ATTRIBUTE_TYPE_BOOLEAN => Marshal.SizeOf<ulong>(),
		CLAIM_SECURITY_ATTRIBUTE_TYPE.CLAIM_SECURITY_ATTRIBUTE_TYPE_STRING => IntPtr.Size,
		_ => 0,
	};
	return (uint)Size;
}

class RESOURCE_ATTRIBUTE(string n, CLAIM_SECURITY_ATTRIBUTE_FLAG f, CLAIM_SECURITY_ATTRIBUTE_TYPE t, string[] v)
{
	public string Name = n;
	public CLAIM_SECURITY_ATTRIBUTE_FLAG Flags = f;
	public CLAIM_SECURITY_ATTRIBUTE_TYPE Type = t;
	public string[] Values = v;
}