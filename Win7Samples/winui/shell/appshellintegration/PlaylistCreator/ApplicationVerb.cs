using Vanara.Extensions;
using Vanara.Extensions.Reflection;
using static Vanara.PInvoke.Ole32;
using static Vanara.PInvoke.OleAut32;
using static Vanara.PInvoke.Shell32;
using static Vanara.PInvoke.ShlwApi;

namespace Vanara.PInvoke;

// CApplicationVerb is a template class that is used to implement shell verbs that integrate with an application using the DelegateExecute
// verb invoke method. the DelegateExecute method has a number of advantages over CommandLine and ContextMenuHandler methods discussed in the
// shell verb implementers guide. this could be adapted to use the DropTarget method to enable operation on Windows XP
//
// this design assumes that the application is expressed as a C++ class with methods that the verb implementation can call to do its work
//
// to use this class
// 1) run uuidgen.exe to generate a new CLSID for each verb you will implement
//
// 2) for each verb define a class using CApplicationVerb<>, provide the CLSID of the verb using __declspec(uuid()), declare the DoVerb()
// method this can be a nested class in the application class.
//
// [ComVisible(true), Guid("B011CE4C-1C1B-4A68-9240-D1D8866537E9")]
// public class CPlayerApplication : CApplicationVerb<CPlaylistCreatorApp, CCreateM3UPlaylistVerb> {
//    protected override void DoVerb(IShellItemArray psia) => Application?.CreatePlaylistAsync(WM_APP_CREATE_M3U, psia);
// }
//
// 3) provide the implementation of the verb by implementing DoVerb(), this can be inline in the class declaration
//
// 4) delcare an instance of each verb in your application class
//
// class CPlayerApplication { private: CPlayVerb _verbPlay; };
//
// 5) set the host application on the CPlayVerb members in the contructor of the class that hosts it
//
// _verbPlay.SetApplication(this);
//
// 6) at application startup time call CApplicationVerb<>.Register()
//
// void CPlayerApplication::_OnInitDlg() { _verbPlay.Register(); }
//
// 7) at app shutdown time call CApplicationVerb<>.UnRegister() to unregister the verb
//
// void CPlayerApplication::_OnDestroyDlg() { _verbPlay.UnRegister(); }
//
// 7) register the verbs in the registry as COM local servers that launches your application. note your app will get passed the "-Embedding"
// when COM launches you. you can use CRegisterExtension.RegisterAppAsLocalServer() to do this

// template parameters:
//
// TApplication - the application class that provides methods for the verb implementation to call to do its work
//
// TVerb - the verb class itself, used to determine the CLSID value of the COM object that implements the verb
//
// this class combines the drop target and the class factory into a single object and it expects to be declared as a member variable of the
// application calls giving it a lifetime that matches the application
/// <summary>Initializes a new instance of the <see cref="CApplicationVerb{TApplication, TVerb}"/> class.</summary>
/// <param name="flags">The flags.</param>
[ClassInterface(ClassInterfaceType.None)]
public abstract class CApplicationVerb<TApplication, TVerb>(CApplicationVerb<TApplication, TVerb>.APPLICATION_VERB_FLAGS flags = CApplicationVerb<TApplication, TVerb>.APPLICATION_VERB_FLAGS.AVF_DEFAULT)
	: IExecuteCommand, IInitializeCommand, IObjectWithSelection, IObjectWithSite, IClassFactory, INamespaceWalkCB2, IDisposable where TApplication : class
{
	private uint _dwRegisterClass;
	private IShellItemArray? _psia;
	private object? _punkSite;

	/// <summary>Specifies flags that control the behavior of application verbs.</summary>
	/// <remarks>
	/// Use these flags to modify how an application verb is invoked or interpreted. Multiple flags can be combined using a bitwise OR
	/// operation to specify multiple behaviors.
	/// </remarks>
	public enum APPLICATION_VERB_FLAGS
	{
		/// <summary>Default behavior. No special flags are set.</summary>
		AVF_DEFAULT = 0x0000,

		/// <summary>
		/// Invoke the verb using the asynchronous methods provided by the application. This allows for better performance and responsiveness
		/// when processing large numbers of items.
		/// </summary>
		AVF_ASYNC = 0x0001,

		/// <summary>
		/// When combined with <see cref="AVF_ASYNC"/>, this flag indicates that if the verb is invoked on multiple items and the
		/// implementation returns S_OK for one item, it implies that the verb should be invoked on all items. This can be used to optimize
		/// performance by reducing the number of calls to the verb implementation.
		/// </summary>
		AVF_ONE_IMPLIES_ALL = 0x0002,
	}

	/// <summary>Gets or sets the application the verb implementations call methods on. Set this value when constructing inherited classes.</summary>
	/// <value>The application.</value>
	public TApplication? Application { get; set; }

	/// <summary>Gets the state of the mouse button from the verb action.</summary>
	/// <value>The state of the mouse button.</value>
	public MouseButtonState KeyState { get; private set; }

	/// <summary>Registers this instance so that other applications can connect to it.</summary>
	public void Register() => CoRegisterClassObject(typeof(TVerb).GUID, this, CLSCTX.CLSCTX_LOCAL_SERVER, REGCLS.REGCLS_MULTIPLEUSE, out _dwRegisterClass);

	/// <summary>Unregisters this instance so that other applications can no longer connect to it.</summary>
	public void Unregister()
	{
		if (_dwRegisterClass != 0)
		{
			CoRevokeClassObject(_dwRegisterClass);
			_dwRegisterClass = 0;
		}
	}

	/// <summary>Called when a verb is being invoked synchronously with the items implement this for sync verbs.</summary>
	/// <param name="psia">The <see cref="IShellItemArray"/> value passed to the verb.</param>
	protected abstract void DoVerb(IShellItemArray psia);

	/// <summary>On an async verb call, this method is called when the discovery of items is complete.</summary>
	protected virtual void EndVerb() { }

	/// <summary>On an async verb call, this method is called when an item is discovered.</summary>
	/// <param name="psi">The discovered <see cref="IShellItem"/> value.</param>
	protected virtual void OnItem(IShellItem psi) { }

	/// <summary>As the items are being discovered, this method is called to see if the discovery should continue.</summary>
	/// <returns><see langword="true"/> to continue discovery; <see langword="false"/> to end it.</returns>
	protected virtual bool ShouldContinue() => _dwRegisterClass != 0;

	/// <summary>On an async verb call, this method is called when the discovery of items starts.</summary>
	protected virtual void StartVerb() { }

	/// <inheritdoc/>
	HRESULT IClassFactory.CreateInstance(object? punkOuter, in Guid riid, out object? ppv)
	{
		System.Diagnostics.Debugger.Break();
		ppv = null;
		return punkOuter is not null ? (HRESULT)HRESULT.CLASS_E_NOAGGREGATION : ShellHelpers.QueryInterface(this, riid, out ppv);
	}

	/// <inheritdoc/>
	void IDisposable.Dispose()
	{
		_psia = null;
		_punkSite = null;
		GC.SuppressFinalize(this);
	}

	/// <inheritdoc/>
	HRESULT INamespaceWalkCB.EnterFolder(IShellFolder psf, [In] IntPtr pidl) => ShouldContinue() ? HRESULT.S_OK : ShellHelpers.HRESULT_FROM_WIN32(Win32Error.ERROR_CANCELLED);

	/// <inheritdoc/>
	HRESULT INamespaceWalkCB2.EnterFolder(IShellFolder psf, IntPtr pidl) => ((INamespaceWalkCB)this).EnterFolder(psf, pidl);

	/// <inheritdoc/>
	HRESULT IExecuteCommand.Execute()
	{
		HRESULT hr;
		if (flags.IsFlagSet(APPLICATION_VERB_FLAGS.AVF_ASYNC))
		{
			INamespaceWalk pnsw = new();
			StartVerb();

			// try to get the items from the view if they are available
			if ((hr = IUnknown_QueryService(_punkSite!, typeof(IFolderView).GUID, typeof(IShellView).GUID, out var psv)).Failed)
				return hr;

			var walkFlags = NAMESPACEWALKFLAG.NSWF_DONT_ACCUMULATE_RESULT | NAMESPACEWALKFLAG.NSWF_ASYNC | NAMESPACEWALKFLAG.NSWF_FLAG_VIEWORDER;
			if (flags.IsFlagSet(APPLICATION_VERB_FLAGS.AVF_ONE_IMPLIES_ALL)) walkFlags |= NAMESPACEWALKFLAG.NSWF_ONE_IMPLIES_ALL;
			const int walkDepth = 8;
			if ((hr = pnsw.Walk(psv ?? _psia!, walkFlags, walkDepth, this)).Failed)
				return hr;
		}
		else if (_psia != null)
		{
			DoVerb(_psia);      // provided by the class using this template
		}
		return HRESULT.S_OK;
	}

	/// <inheritdoc/>
	HRESULT INamespaceWalkCB.FoundItem(IShellFolder psf, IntPtr pidl)
	{
		var hr = SHCreateItemWithParent(PIDL.Null, psf, pidl, typeof(IShellItem2).GUID, out var ppvItem);
		if (hr.Succeeded)
		{
			OnItem((IShellItem2)ppvItem!);
		}
		return ShouldContinue() ? hr : ShellHelpers.HRESULT_FROM_WIN32(Win32Error.ERROR_CANCELLED);
	}

	/// <inheritdoc/>
	HRESULT INamespaceWalkCB2.FoundItem(IShellFolder psf, IntPtr pidl) => ((INamespaceWalkCB)this).FoundItem(psf, pidl);

	/// <inheritdoc/>
	HRESULT IObjectWithSelection.GetSelection(in Guid riid, out object? ppv)
	{
		ppv = null;
		return _psia != null ? ShellHelpers.QueryInterface(_psia, riid, out ppv) : HRESULT.E_FAIL;
	}

	/// <inheritdoc/>
	HRESULT IObjectWithSite.GetSite(in Guid riid, out object? ppv)
	{
		ppv = null;
		return _psia != null ? ShellHelpers.QueryInterface(_punkSite!, riid, out ppv) : HRESULT.E_FAIL;
	}

	/// <inheritdoc/>
	HRESULT IInitializeCommand.Initialize(string pszCommandName, IPropertyBag ppb) => HRESULT.S_OK;

	/// <inheritdoc/>
	HRESULT INamespaceWalkCB.InitializeProgressDialog(out string? ppszTitle, out string? ppszCancel)
	{
		ppszTitle = ppszCancel = null;
		return HRESULT.E_NOTIMPL;
	}

	/// <inheritdoc/>
	HRESULT INamespaceWalkCB2.InitializeProgressDialog(out string? ppszTitle, out string? ppszCancel) => ((INamespaceWalkCB)this).InitializeProgressDialog(out ppszTitle, out ppszCancel);

	/// <inheritdoc/>
	HRESULT INamespaceWalkCB.LeaveFolder(IShellFolder psf, [In] IntPtr pidl) => ShouldContinue() ? HRESULT.S_OK : ShellHelpers.HRESULT_FROM_WIN32(Win32Error.ERROR_CANCELLED);

	/// <inheritdoc/>
	HRESULT INamespaceWalkCB2.LeaveFolder(IShellFolder psf, IntPtr pidl) => ((INamespaceWalkCB)this).LeaveFolder(psf, pidl);

	/// <inheritdoc/>
	HRESULT IClassFactory.LockServer(bool _) => HRESULT.S_OK;

	/// <inheritdoc/>
	HRESULT IExecuteCommand.SetDirectory([In, MarshalAs(UnmanagedType.LPWStr)] string? _) => HRESULT.S_OK;

	/// <inheritdoc/>
	HRESULT IExecuteCommand.SetKeyState(MouseButtonState grfKeyState)
	{
		KeyState = grfKeyState;
		return HRESULT.S_OK;
	}

	/// <inheritdoc/>
	HRESULT IExecuteCommand.SetNoShowUI([MarshalAs(UnmanagedType.Bool)] bool _) => HRESULT.S_OK;

	/// <inheritdoc/>
	HRESULT IExecuteCommand.SetParameters([In, MarshalAs(UnmanagedType.LPWStr)] string _) => HRESULT.S_OK;

	/// <inheritdoc/>
	HRESULT IExecuteCommand.SetPosition(POINT _) => HRESULT.S_OK;

	/// <inheritdoc/>
	HRESULT IObjectWithSelection.SetSelection(IShellItemArray psia)
	{
		_psia = psia;
		return HRESULT.S_OK;
	}

	/// <inheritdoc/>
	HRESULT IExecuteCommand.SetShowWindow(ShowWindowCommand nShow) => HRESULT.S_OK;

	/// <inheritdoc/>
	HRESULT IObjectWithSite.SetSite(object? punkSite)
	{
		_punkSite = punkSite;
		try { Application.InvokeMethod("SetSite", punkSite); } catch { }
		return HRESULT.S_OK;
	}

	/// <inheritdoc/>
	HRESULT INamespaceWalkCB2.WalkComplete(HRESULT hr)
	{
		EndVerb();
		return HRESULT.S_OK;
	}
}