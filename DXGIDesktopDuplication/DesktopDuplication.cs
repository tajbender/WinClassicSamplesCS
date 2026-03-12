global using Vanara.Extensions;
global using Vanara.InteropServices;
global using Vanara.PInvoke;
global using static Vanara.PInvoke.Kernel32;
global using static Vanara.PInvoke.User32;
global using static Vanara.PInvoke.D3D11;
global using static Vanara.PInvoke.DXGI;
global using static Vanara.PInvoke.DirectXMath;

internal static class Program
{
	public const int NUMVERTICES = 6;
	public const int BPP = 4;
	public const int OCCLUSION_STATUS_MSG = (int)WindowMessage.WM_USER;

	//
	// Globals
	//
	internal static OUTPUTMANAGER OutMgr = new();

	// Below are lists of errors expect from Dxgi API calls when a transition event like mode change, PnpStop, PnpStart
	// desktop switch, TDR or session disconnect/reconnect. In all these cases we want the application to clean up the threads that process
	// the desktop updates and attempt to recreate them.
	// If we get an error that is not on the appropriate list then we exit the application

	// These are the errors we expect from general Dxgi API due to a transition
	internal static HRESULT[] SystemTransitionsExpectedErrors = [
												HRESULT.DXGI_ERROR_DEVICE_REMOVED,
												HRESULT.DXGI_ERROR_ACCESS_LOST,
												(int)WAIT_STATUS.WAIT_ABANDONED,
												HRESULT.S_OK                                    // Terminate list with zero valued HRESULT
											];

	// These are the errors we expect from IDXGIOutput1::DuplicateOutput due to a transition
	internal static HRESULT[] CreateDuplicationExpectedErrors = [
												HRESULT.DXGI_ERROR_DEVICE_REMOVED,
												HRESULT.E_ACCESSDENIED,
												HRESULT.DXGI_ERROR_UNSUPPORTED,
												HRESULT.DXGI_ERROR_SESSION_DISCONNECTED,
												HRESULT.S_OK                                    // Terminate list with zero valued HRESULT
											];

	// These are the errors we expect from IDXGIOutputDuplication methods due to a transition
	internal static HRESULT[] FrameInfoExpectedErrors = [
										HRESULT.DXGI_ERROR_DEVICE_REMOVED,
										HRESULT.DXGI_ERROR_ACCESS_LOST,
										HRESULT.S_OK                                    // Terminate list with zero valued HRESULT
									];

	// These are the errors we expect from IDXGIAdapter::EnumOutputs methods due to outputs becoming stale during a transition
	internal static HRESULT[] EnumOutputsExpectedErrors = [
										  HRESULT.DXGI_ERROR_NOT_FOUND,
										  HRESULT.S_OK                                    // Terminate list with zero valued HRESULT
									  ];

	internal static ID3DBlob g_VS, g_PS;

	static Program()
	{
		HRESULT hr = D3DCompiler.D3DCompileFromFile("VertexShader.hlsl", default, default, "VSMain", "vs_5_0", 0, 0, out g_VS);
		if (hr.Failed)
		{
			ProcessFailure(null, "Failed to compile vertex shader", "Error", hr, SystemTransitionsExpectedErrors);
			throw new InvalidOperationException();
		}
		hr = D3DCompiler.D3DCompileFromFile("PixelShader.hlsl", default, default, "PSMain", "ps_5_0", 0, 0, out g_PS);
		if (hr.Failed)
		{
			ProcessFailure(null, "Failed to compile pixel shader", "Error", hr, SystemTransitionsExpectedErrors);
			throw new InvalidOperationException();
		}
	}

	static int Main(string[] args)
	{
		bool CmdResult = ProcessCmdline(out var SingleOutput);
		if (!CmdResult)
		{
			ShowHelp();
			return 0;
		}

		// Event used by the threads to signal an unexpected error and we want to quit the app
		var UnexpectedErrorEvent = CreateEvent(default, true, false, default);
		if (!UnexpectedErrorEvent)
		{
			ProcessFailure(default, "UnexpectedErrorEvent creation failed", "Error", HRESULT.E_UNEXPECTED);
			return 0;
		}

		// Event for when a thread encounters an expected error
		var ExpectedErrorEvent = CreateEvent(default, true, false, default);
		if (!ExpectedErrorEvent)
		{
			ProcessFailure(default, "ExpectedErrorEvent creation failed", "Error", HRESULT.E_UNEXPECTED);
			return 0;
		}

		// Event to tell spawned threads to quit
		var TerminateThreadsEvent = CreateEvent(default, true, false, default);
		if (!TerminateThreadsEvent)
		{
			ProcessFailure(default, "TerminateThreadsEvent creation failed", "Error", HRESULT.E_UNEXPECTED);
			return 0;
		}

		// Register class
		WindowClass Wc = new("ddasample", default, WndProc, WindowClassStyles.CS_HREDRAW | WindowClassStyles.CS_VREDRAW);

		// Create window
		RECT WindowRect = new(0, 0, 800, 600);
		AdjustWindowRect(ref WindowRect, WindowStyles.WS_OVERLAPPEDWINDOW, false);
		var WindowHandle = CreateWindow("ddasample", "DXGI desktop duplication sample", WindowStyles.WS_OVERLAPPEDWINDOW, 0, 0,
			WindowRect.right - WindowRect.left, WindowRect.bottom - WindowRect.top);
		if (!WindowHandle)
		{
			ProcessFailure(default, "Window creation failed", "Error", HRESULT.E_FAIL);
			return 0;
		}

		ShowWindow(WindowHandle, ShowWindowCommand.SW_NORMAL);
		UpdateWindow(WindowHandle);

		THREADMANAGER ThreadMgr = new();

		// Message loop (attempts to update screen when no other messages to process)
		MSG msg = default;
		bool FirstTime = true;
		bool Occluded = true;
		DYNAMIC_WAIT DynamicWait = new();

		while ((uint)WindowMessage.WM_QUIT != msg.message)
		{
			DUPL_RETURN Ret = DUPL_RETURN.DUPL_RETURN_SUCCESS;
			if (PeekMessage(out msg, default, 0, 0, PM.PM_REMOVE))
			{
				if (msg.message == OCCLUSION_STATUS_MSG)
					// Present may not be occluded now so try again
					Occluded = false;
				else
				{
					// Process window messages
					TranslateMessage(msg);
					DispatchMessage(msg);
				}
			}
			else if (WaitForSingleObjectEx(UnexpectedErrorEvent, 0, false) == WAIT_STATUS.WAIT_OBJECT_0)
			{
				// Unexpected error occurred so exit the application
				break;
			}
			else if (FirstTime || WaitForSingleObjectEx(ExpectedErrorEvent, 0, false) == WAIT_STATUS.WAIT_OBJECT_0)
			{
				if (!FirstTime)
				{
					// Terminate other threads
					SetEvent(TerminateThreadsEvent);
					ThreadMgr.WaitForThreadTermination();
					ResetEvent(TerminateThreadsEvent);
					ResetEvent(ExpectedErrorEvent);

					// Clean up
					ThreadMgr.Clean();
					OutMgr.CleanRefs();

					// As we have encountered an error due to a system transition we wait before trying again, using this dynamic wait
					// the wait periods will get progressively long to avoid wasting too much system resource if this state lasts a long time
					DynamicWait.Wait();
				}
				else
				{
					// First time through the loop so nothing to clean up
					FirstTime = false;
				}

				// Re-initialize
				Ret = OutMgr.InitOutput(WindowHandle, SingleOutput, out var OutputCount, out var DeskBounds);
				if (Ret == DUPL_RETURN.DUPL_RETURN_SUCCESS)
				{
					HANDLE SharedHandle = OutMgr.GetSharedHandle();
					if (!SharedHandle.IsNull)
						Ret = ThreadMgr.Initialize(SingleOutput, (int)OutputCount, UnexpectedErrorEvent, ExpectedErrorEvent, TerminateThreadsEvent, SharedHandle, DeskBounds);
					else
					{
						DisplayMsg("Failed to get handle of shared surface", "Error", HRESULT.S_OK);
						Ret = DUPL_RETURN.DUPL_RETURN_ERROR_UNEXPECTED;
					}
				}

				// We start off in occluded state and we should immediate get a occlusion status window message
				Occluded = true;
			}
			else
			{
				// Nothing else to do, so try to present to write out to window if not occluded
				if (!Occluded)
					Ret = OutMgr.UpdateApplicationWindow(ThreadMgr.GetPointerInfo(), ref Occluded);
			}

			// Check if for errors
			if (Ret != DUPL_RETURN.DUPL_RETURN_SUCCESS)
			{
				if (Ret == DUPL_RETURN.DUPL_RETURN_ERROR_EXPECTED)
					// Some type of system transition is occurring so retry
					SetEvent(ExpectedErrorEvent);
				else
				{
					// Unexpected error so exit
					break;
				}
			}
		}

		// Make sure all other threads have exited
		if (SetEvent(TerminateThreadsEvent))
			ThreadMgr.WaitForThreadTermination();

		if (msg.message == (uint)WindowMessage.WM_QUIT)
			// For a WM_QUIT message we should return the wParam value
			return (int)msg.wParam;

		return 0;
	}

	internal static DUPL_RETURN ProcessFailure([In, Optional] ID3D11Device? Device, string Str, string Title, HRESULT hr, [In, Optional] HRESULT[]? ExpectedErrors)
	{
		HRESULT TranslatedHr;

		// On an error check if the DX device is lost
		if (Device is not null)
		{
			HRESULT DeviceRemovedReason = Device.GetDeviceRemovedReason();
			TranslatedHr = (int)DeviceRemovedReason switch
			{
				// Our device has been stopped due to an external event on the GPU so map them all to device removed and continue processing the condition
				HRESULT.DXGI_ERROR_DEVICE_REMOVED or HRESULT.DXGI_ERROR_DEVICE_RESET or HRESULT.E_OUTOFMEMORY => (HRESULT)HRESULT.DXGI_ERROR_DEVICE_REMOVED,
				// Device is not removed so use original error
				HRESULT.S_OK => hr,
				// Device is removed but not a error we want to remap
				_ => DeviceRemovedReason,
			};
		}
		else
		{
			TranslatedHr = hr;
		}

		// Check if this error was expected or not
		if (ExpectedErrors is not null && ExpectedErrors.Any(e => e == TranslatedHr))
			return DUPL_RETURN.DUPL_RETURN_ERROR_EXPECTED;

		// Error was not expected so display the message box
		DisplayMsg(Str, Title, TranslatedHr);

		return DUPL_RETURN.DUPL_RETURN_ERROR_UNEXPECTED;
	}

	//
	// Displays a message
	//
	internal static void DisplayMsg(string Str, string Title, HRESULT hr) =>
		MessageBox(default, hr.Succeeded ? Str : $"{Str} with 0x{(int)hr:X}", Title, MB_FLAGS.MB_OK);

	//
	// Shows help
	//
	static void ShowHelp() =>
		DisplayMsg("The following optional parameters can be used -\n /output [all | n]\t\tto duplicate all outputs or the nth output\n /?\t\t\tto display this help section",
			"Proper usage", HRESULT.S_OK);

	//
	// Process command line parameters
	//
	static bool ProcessCmdline(out int Output)
	{
		Output = -1;

		string[] args = Environment.GetCommandLineArgs();

		// _argv and _argc are global vars set by system
		for (uint i = 0; i < args.Length; ++i)
		{
			if (args[i] is "-output" or "/output")
			{
				if (++i >= args.Length)
					return false;

				Output = args[i] is "all" ? -1 : int.Parse(args[i]);

				continue;
			}
			else
			{
				return false;
			}
		}
		return true;
	}

	//
	// Window message processor
	//
	static nint WndProc(HWND hWnd, uint message, nint wParam, nint lParam)
	{
		switch ((WindowMessage)message)
		{
			case WindowMessage.WM_DESTROY:
				PostQuitMessage(0);
				break;

			case WindowMessage.WM_SIZE:
				// Tell output manager that window size has changed
				OutMgr.WindowResize();
				break;

			default:
				return DefWindowProc(hWnd, message, wParam, lParam);
		}

		return 0;
	}

	//
	// Entry point for new duplication threads
	//
	internal static uint DDProc([In] IntPtr Param)
	{
		// Data passed in from thread creation
		THREAD_DATA TData = (THREAD_DATA)GCHandle.FromIntPtr(Param).Target!;

		// Get desktop
		DUPL_RETURN Ret;
		using (var CurrentDesktop = OpenInputDesktop(0, false, ACCESS_MASK.GENERIC_ALL))
		{
			if (CurrentDesktop.IsInvalid)
			{
				// We do not have access to the desktop so request a retry
				TData.ExpectedErrorEvent.Set();
				Ret = DUPL_RETURN.DUPL_RETURN_ERROR_EXPECTED;
				goto Exit;
			}

			// Attach desktop to this thread
			if (!SetThreadDesktop(CurrentDesktop))
			{
				// We do not have access to the desktop so request a retry
				Ret = DUPL_RETURN.DUPL_RETURN_ERROR_EXPECTED;
				goto Exit;
			}
		}

		// New display manager
		DISPLAYMANAGER DispMgr = new();
		DispMgr.InitD3D(TData.DxRes);

		// Obtain handle to sync shared Surface
		var hr = FunctionHelper.IidGetObj(TData.DxRes.Device!.OpenSharedResource, (IntPtr)TData.TexSharedHandle, out ID3D11Texture2D? SharedSurf);
		if (hr.Failed)
		{
			Ret = ProcessFailure(TData.DxRes.Device, "Opening shared texture failed", "Error", hr, SystemTransitionsExpectedErrors);
			goto Exit;
		}

		IDXGIKeyedMutex? KeyMutex = SharedSurf as IDXGIKeyedMutex;
		if (KeyMutex is null)
		{
			Ret = ProcessFailure(default, "Failed to get keyed mutex interface in spawned thread", "Error", hr);
			goto Exit;
		}

		// Make duplication manager
		DUPLICATIONMANAGER DuplMgr = new();
		Ret = DuplMgr.InitDupl(TData.DxRes.Device, TData.Output);
		if (Ret != DUPL_RETURN.DUPL_RETURN_SUCCESS)
		{
			goto Exit;
		}

		// Get output description
		DuplMgr.GetOutputDesc(out var DesktopDesc);

		// Main duplication loop
		bool WaitToProcessCurrentFrame = false;
		FRAME_DATA? CurrentData = default;

		while ((WaitForSingleObjectEx(TData.TerminateThreadsEvent, 0, false) == WAIT_STATUS.WAIT_TIMEOUT))
		{
			if (!WaitToProcessCurrentFrame)
			{
				// Get new frame from desktop duplication
				Ret = DuplMgr.GetFrame(out CurrentData, out var TimeOut);
				if (Ret != DUPL_RETURN.DUPL_RETURN_SUCCESS)
				{
					// An error occurred getting the next frame drop out of loop which
					// will check if it was expected or not
					break;
				}

				// Check for timeout
				if (TimeOut)
				{
					// No new frame at the moment
					continue;
				}
			}

			// We have a new frame so try and process it
			// Try to acquire keyed mutex in order to access shared surface
			hr = KeyMutex.AcquireSync(0, 1000);
			if (hr == (int)(WAIT_STATUS.WAIT_TIMEOUT))
			{
				// Can't use shared surface right now, try again later
				WaitToProcessCurrentFrame = true;
				continue;
			}
			else if (hr.Failed)
			{
				// Generic unknown failure
				Ret = ProcessFailure(TData.DxRes.Device, "Unexpected error acquiring KeyMutex", "Error", hr, SystemTransitionsExpectedErrors);
				DuplMgr.DoneWithFrame();
				break;
			}

			// We can now process the current frame
			WaitToProcessCurrentFrame = false;

			// Get mouse info
			Ret = DuplMgr.GetMouse(TData.PtrInfo!, CurrentData!.FrameInfo, TData.OffsetX, TData.OffsetY);
			if (Ret != DUPL_RETURN.DUPL_RETURN_SUCCESS)
			{
				DuplMgr.DoneWithFrame();
				KeyMutex.ReleaseSync(1);
				break;
			}

			// Process new frame
			Ret = DispMgr.ProcessFrame(CurrentData, SharedSurf!, TData.OffsetX, TData.OffsetY, DesktopDesc);
			if (Ret != DUPL_RETURN.DUPL_RETURN_SUCCESS)
			{
				DuplMgr.DoneWithFrame();
				KeyMutex.ReleaseSync(1);
				break;
			}

			// Release acquired keyed mutex
			hr = KeyMutex.ReleaseSync(1);
			if (hr.Failed)
			{
				Ret = ProcessFailure(TData.DxRes.Device, "Unexpected error releasing the keyed mutex", "Error", hr, SystemTransitionsExpectedErrors);
				DuplMgr.DoneWithFrame();
				break;
			}

			// Release frame back to desktop duplication
			Ret = DuplMgr.DoneWithFrame();
			if (Ret != DUPL_RETURN.DUPL_RETURN_SUCCESS)
			{
				break;
			}
		}

Exit:
		if (Ret != DUPL_RETURN.DUPL_RETURN_SUCCESS)
		{
			if (Ret == DUPL_RETURN.DUPL_RETURN_ERROR_EXPECTED)
			{
				// The system is in a transition state so request the duplication be restarted
				TData.ExpectedErrorEvent.Set();
			}
			else
			{
				// Unexpected error so exit the application
				TData.UnexpectedErrorEvent.Set();
			}
		}

		SharedSurf = default;
		KeyMutex = default;

		return 0;
	}
}

//
// Class for progressive waits
//
public struct WAIT_BAND(uint t, uint c)
{
	public uint WaitTime = t;
	public uint WaitCount = c;
}

internal class DYNAMIC_WAIT
{
	// Period in seconds that a new wait call is considered part of the same wait sequence
	const uint m_WaitSequenceTimeInSeconds = 2;
	const int WAIT_BAND_STOP = 0;

	uint m_CurrentWaitBandIdx;
	uint m_WaitCountInCurrentBand;
	readonly long m_QPCFrequency;
	long m_LastWakeUpTime;
	readonly bool m_QPCValid;

	static readonly WAIT_BAND[] m_WaitBands = [
		new(250, 20),
		new(2000, 60),
		new(5000, WAIT_BAND_STOP) // Never move past this band
	];

	public DYNAMIC_WAIT()
	{
		m_QPCValid = QueryPerformanceFrequency(out m_QPCFrequency);
		m_LastWakeUpTime = 0;
	}
	
	public void Wait()
	{
		// Is this wait being called with the period that we consider it to be part of the same wait sequence
		QueryPerformanceCounter(out var CurrentQPC);
		if (m_QPCValid && (CurrentQPC <= (m_LastWakeUpTime + (m_QPCFrequency * m_WaitSequenceTimeInSeconds))))
		{
			// We are still in the same wait sequence, lets check if we should move to the next band
			if ((m_WaitBands[m_CurrentWaitBandIdx].WaitCount != WAIT_BAND_STOP) && (m_WaitCountInCurrentBand > m_WaitBands[m_CurrentWaitBandIdx].WaitCount))
			{
				m_CurrentWaitBandIdx++;
				m_WaitCountInCurrentBand = 0;
			}
		}
		else
		{
			// Either we could not get the current time or we are starting a new wait sequence
			m_WaitCountInCurrentBand = 0;
			m_CurrentWaitBandIdx = 0;
		}

		// Sleep for the required period of time
		Sleep(m_WaitBands[m_CurrentWaitBandIdx].WaitTime);

		// Record the time we woke up so we can detect wait sequences
		QueryPerformanceCounter(out m_LastWakeUpTime);
		m_WaitCountInCurrentBand++;
	}
}