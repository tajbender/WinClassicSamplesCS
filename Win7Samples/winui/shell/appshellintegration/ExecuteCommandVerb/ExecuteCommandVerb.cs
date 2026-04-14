using System.Diagnostics.CodeAnalysis;
using Vanara.PInvoke;
using static Vanara.PInvoke.Ole32;
using static Vanara.PInvoke.Shell32;
using static Vanara.PInvoke.ShellHelpers;
using static Vanara.PInvoke.ShlwApi;
using static Vanara.PInvoke.User32;

const string c_szVerbDisplayName = "ExecuteCommand Verb Sample";
const string c_szProgID = "txtfile";
const string c_szVerbName = "ExecuteCommandVerb";

if (Environment.CommandLine.Contains("-embedding", StringComparison.CurrentCultureIgnoreCase))
{
	new CExecuteCommandVerb().Run();
}
else if (Environment.CommandLine.Contains("-unreg", StringComparison.CurrentCultureIgnoreCase))
{
	CRegisterExtension.Create<CExecuteCommandVerb>().UnRegisterObject();
}
else
{
	var re = CRegisterExtension.Create<CExecuteCommandVerb>();

	var hr = re.RegisterAppAsLocalServer(c_szVerbDisplayName);
	if (hr.Succeeded)
	{

		// register this verb on .txt files ProgID
		hr = re.RegisterExecuteCommandVerb(c_szProgID, c_szVerbName, c_szVerbDisplayName);
		if (hr.Succeeded)
		{
			hr = re.RegisterVerbAttribute(c_szProgID, c_szVerbName, "NeverDefault");
			if (hr.Succeeded)
			{
				MessageBox(default, "Installed ExecuteCommand Verb Sample for .txt files\n\n" +
					"right click on a .txt file and choose 'ExecuteCommand Verb Sample' to see this in action",
					c_szVerbDisplayName, MB_FLAGS.MB_OK);
			}
		}
	}
}

internal class CExecuteCommandVerb : DropTargetVerb.CAppMessageLoop, IExecuteCommand, IObjectWithSelection, IInitializeCommand, IObjectWithSite, IDisposable
{
	private MouseButtonState grfKeyState;
	private HWND hwnd;
	private ShowWindowCommand nShow;
	private IShellItemArray? psia = null;
	private POINT pt;
	private object? punkSite = null;

	public HRESULT Run()
	{
		new DropTargetVerb.CStaticClassFactory<CExecuteCommandVerb>(this).Register();
		MessageLoop();
		return HRESULT.S_OK;
	}

	void IDisposable.Dispose() => punkSite = psia = null;

	HRESULT IExecuteCommand.Execute()
	{
		// capture state from the site needed to invoke the verb here note the HWND can be retrieved here but it should not be used for modal
		// UI as all shell verbs should be modeless as well as not block the caller
		IUnknown_GetWindow(punkSite!, out hwnd);

		// queue the execution of the verb via the message pump
		QueueAppCallback();

		return HRESULT.S_OK;
	}

	HRESULT IObjectWithSelection.GetSelection(in Guid riid, [MaybeNull] out object? ppv) { ppv = null; return psia is not null ? QueryInterface(psia!, riid, out ppv) : HRESULT.E_FAIL; }

	HRESULT IObjectWithSite.GetSite(in Guid riid, out object? ppv) { ppv = null; return punkSite is not null ? QueryInterface(punkSite!, riid, out ppv) : HRESULT.E_FAIL; }

	HRESULT IInitializeCommand.Initialize(string pszCommandName, OleAut32.IPropertyBag ppb) => HRESULT.S_OK;

	HRESULT IExecuteCommand.SetDirectory(string? pszDirectory) => HRESULT.S_OK;

	HRESULT IExecuteCommand.SetKeyState(MouseButtonState grfKeyState) { this.grfKeyState = grfKeyState; return HRESULT.S_OK; }

	HRESULT IExecuteCommand.SetNoShowUI(bool fNoShowUI) => HRESULT.S_OK;

	HRESULT IExecuteCommand.SetParameters(string pszParameters) => HRESULT.S_OK;

	HRESULT IExecuteCommand.SetPosition(POINT pt) { this.pt = pt; return HRESULT.S_OK; }

	HRESULT IObjectWithSelection.SetSelection(IShellItemArray psia) { this.psia = psia; return HRESULT.S_OK; }

	HRESULT IExecuteCommand.SetShowWindow(ShowWindowCommand nShow) { this.nShow = nShow; return HRESULT.S_OK; }

	HRESULT IObjectWithSite.SetSite(object? pUnkSite) { this.punkSite = pUnkSite; return HRESULT.S_OK; }

	protected override void OnAppCallback()
	{
		uint count = psia!.GetCount();

		HRESULT hr = GetItemAt(psia, 0, out IShellItem2? psi);
		if (hr.Succeeded)
		{
			string pszName = psi!.GetDisplayName(SIGDN.SIGDN_PARENTRELATIVEPARSING);
			string szMsg = $"{count} item(s), first item is named {pszName}";
			MessageBox(default, szMsg, "ExecuteCommand Verb Sample", MB_FLAGS.MB_OK | MB_FLAGS.MB_SETFOREGROUND);
		}
	}
}