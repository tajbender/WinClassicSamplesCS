using Vanara.DirectoryServices;
using static Vanara.PInvoke.ActiveDS;
using Vanara.InteropServices;

///////////////////////////////////////////////////
// Create a user using IADsContainer::Create
////////////////////////////////////////////////////
var pCont = (ADsComputer)ADsObject.GetObject($"WinNT://{Environment.MachineName}");

var pIADsUser = (ADsUser)pCont.Children.Add("user", "AliceW");

//do NOT hard code your password into the source code in production code
//take user input instead
pIADsUser.SetPassword("MysEcret1");
pIADsUser.PropertyCache.Save(); // Commit

Console.Write("User created successfully\n");

return 0;

#pragma warning disable CS8321 // Local function is declared but never used
static unsafe void CreateUserIDirectoryObject()
{
	///////////////////////////////////////////////////////////
	// Use IDirectoryObject to create an object
	////////////////////////////////////////////////////////////

	// Create this user in an organizational unit
	var hr = ADsGetObject("LDAP://OU=MyOU,DC=fabrikam,DC=com", out IDirectoryObject? pDirObject);
	if (hr.Succeeded)
	{
		ADS_ATTR_INFO[] attrInfo = [
			new("objectClass", "user"),
			new("sAMAccountName", "mikes"),
			new("userPrincipalName", "mikes@fabrikam.com"),
		];

		_ = pDirObject!.CreateDSObject("CN=Mike Smith", attrInfo);
	}
}
#pragma warning restore CS8321 // Local function is declared but never used