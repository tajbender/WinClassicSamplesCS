using static Vanara.PInvoke.User32;
using static Vanara.PInvoke.Shell32;

// Create the FileOpenDialog object.
IFileOpenDialog pFileOpen = new();

// Show the Open dialog box.
pFileOpen.Show();

try
{
	// Get the file name from the dialog box.
	IShellItem pItem = pFileOpen.GetResult();
	string pszFilePath = pItem.GetDisplayName(SIGDN.SIGDN_FILESYSPATH);

	// Display the file name to the user.
	MessageBox(default, pszFilePath, "File Path", MB_FLAGS.MB_OK);
}
catch { }

return 0;