using System.Diagnostics;

internal class Win32Application : VisibleWindow
{
	private readonly DXSample pSample;
	private RECT m_windowRect;
	private readonly WindowStyles m_windowStyle = WindowStyles.WS_OVERLAPPEDWINDOW;

	private Win32Application(DXSample sample)
	{
		pSample = sample;
		pSample.Win32App = this;
	}

	public bool IsFullscreen { get; private set; } = false;

	public void SetWindowZorderToTopMost(bool setToTopMost)
	{
		GetWindowRect(Handle, out var windowRect);

		SetWindowPos(
			Handle,
			setToTopMost ? HWND.HWND_TOPMOST : HWND.HWND_NOTOPMOST,
			windowRect.left,
			windowRect.top,
			windowRect.right - windowRect.left,
			windowRect.bottom - windowRect.top,
			SetWindowPosFlags.SWP_FRAMECHANGED | SetWindowPosFlags.SWP_NOACTIVATE);
	}

	// Convert a styled window into a fullscreen borderless window and back again.
	public void ToggleFullscreenWindow(IDXGISwapChain? pSwapChain = null)
	{
		if (IsFullscreen)
		{
			// Restore the window's attributes and size.
			SetWindowLong(Handle, WindowLongFlags.GWL_STYLE, (int)m_windowStyle);

			SetWindowPos(
				Handle,
				HWND.HWND_NOTOPMOST,
				m_windowRect.left,
				m_windowRect.top,
				m_windowRect.Width,
				m_windowRect.Height,
				SetWindowPosFlags.SWP_FRAMECHANGED | SetWindowPosFlags.SWP_NOACTIVATE);

			ShowWindow(Handle, ShowWindowCommand.SW_NORMAL);
		}
		else
		{
			// Save the old window rect so we can restore it when exiting fullscreen mode.
			GetWindowRect(Handle, out m_windowRect);

			// Make the window borderless so that the client area can fill the screen.
			SetWindowLong(Handle, WindowLongFlags.GWL_STYLE, (int)(m_windowStyle & ~(WindowStyles.WS_CAPTION | WindowStyles.WS_MAXIMIZEBOX | WindowStyles.WS_MINIMIZEBOX | WindowStyles.WS_SYSMENU | WindowStyles.WS_THICKFRAME)));

			RECT fullscreenWindowRect;
			try
			{
				if (pSwapChain is not null)
				{
					// Get the settings of the display on which the app's window is currently displayed
					IDXGIOutput pOutput = pSwapChain.GetContainingOutput();
					DXGI_OUTPUT_DESC Desc = pOutput.GetDesc();
					fullscreenWindowRect = Desc.DesktopCoordinates;
				}
				else
				{
					// Fallback to EnumDisplaySettings implementation
					throw new Exception();
				}
			}
			catch
			{
				// Get the settings of the primary display
				DEVMODE devMode = DEVMODE.Default;
				EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref devMode);

				fullscreenWindowRect = new(devMode.dmPosition.x, devMode.dmPosition.y, devMode.dmPosition.x + (int)devMode.dmPelsWidth, devMode.dmPosition.y + (int)devMode.dmPelsHeight);
			}

			SetWindowPos(
				Handle,
				HWND.HWND_TOPMOST,
				fullscreenWindowRect.left,
				fullscreenWindowRect.top,
				fullscreenWindowRect.right,
				fullscreenWindowRect.bottom,
				SetWindowPosFlags.SWP_FRAMECHANGED | SetWindowPosFlags.SWP_NOACTIVATE);

			ShowWindow(Handle, ShowWindowCommand.SW_MAXIMIZE);
		}

		IsFullscreen = !IsFullscreen;
	}

	internal static int Run(DXSample sample)
	{
		try
		{
			// Parse the command line parameters
			sample.ParseCommandLineArgs(Environment.GetCommandLineArgs());

			// Initialize the window class.
			WindowClass windowClass = new("DXSampleClass", styles: WindowClassStyles.CS_VREDRAW | WindowClassStyles.CS_HREDRAW);

			// Create the window and store a handle to it.
			RECT windowRect = new(0, 0, sample.Width, sample.Height);
			using Win32Application val = new(sample);
			AdjustWindowRect(ref windowRect, val.m_windowStyle, false);
			val.CreateHandle(windowClass, sample.Title, windowRect.Size, null, val.m_windowStyle);

			// Initialize the sample. OnInit is defined in each child-implementation of DXSample.
			sample.OnInit();

			val.Show();

			// Main sample loop.
			new MessagePump().Run(val);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Application hit a problem: {ex}\nTerminating.");
			return 1;
		}
		finally
		{
			sample.OnDestroy();
		}
		return 0;
	}

	protected override IntPtr WndProc(HWND hwnd, uint msg, IntPtr wParam, IntPtr lParam)
	{
		switch ((WindowMessage)msg)
		{
			case WindowMessage.WM_CREATE:
				// CREATESTRUCT captured already in VisualWindow.CreateParams
				return default;

			case WindowMessage.WM_KEYDOWN:
				return HANDLE_WM_KEYDOWN(hwnd, wParam, lParam, (HWND _, VK vk, WM_KEY_LPARAM _) => pSample.OnKeyDown(vk));

			case WindowMessage.WM_KEYUP:
				return HANDLE_WM_KEYUP(hwnd, wParam, lParam, (HWND _, VK vk, WM_KEY_LPARAM _) => pSample.OnKeyUp(vk));

			case WindowMessage.WM_SYSKEYDOWN:
				bool handled = false;
				HANDLE_WM_SYSKEYDOWN(hwnd, wParam, lParam, (HWND _, VK vk, WM_KEY_LPARAM klm) =>
				{
					// Handle ALT+ENTER:
					if (vk == VK.VK_RETURN && klm.AltKeyDown && pSample.TearingSupport)
					{
						ToggleFullscreenWindow(pSample.GetSwapchain());
						handled = true;
					}
				});
				if (handled)
					return IntPtr.Zero;
				// Send all other WM_SYSKEYDOWN messages to the default WndProc.
				break;

			case WindowMessage.WM_PAINT:
				pSample.OnUpdate();
				pSample.OnRender();
				return default;

			case WindowMessage.WM_SIZE:
				return HANDLE_WM_SIZE(hwnd, wParam, lParam, (HWND _, WM_SIZE_WPARAM type, SIZES sz) =>
				{
					GetWindowRect(hwnd, out var windowRect);
					pSample.WindowBounds = windowRect;

					pSample.OnSizeChanged(sz.Width, sz.Height, type == WM_SIZE_WPARAM.SIZE_MINIMIZED);
				});

			case WindowMessage.WM_MOVE:
				return HANDLE_WM_MOVE(hwnd, wParam, lParam, (HWND _, POINTS pt) =>
				{
					GetWindowRect(hwnd, out var windowRect);
					pSample.WindowBounds = windowRect;

					pSample.OnWindowMoved(pt.x, pt.y);
				});

			case WindowMessage.WM_DISPLAYCHANGE:
				pSample.OnDisplayChanged();
				return default;

			case WindowMessage.WM_MOUSEMOVE:
				return HANDLE_WM_MOUSEMOVE(hwnd, wParam, lParam, (HWND _, MouseButtonState state, POINTS pt) =>
				{
					if (state == MouseButtonState.MK_LBUTTON)
						pSample.OnMouseMove((uint)pt.x, (uint)pt.y);
				});

			case WindowMessage.WM_LBUTTONDOWN:
				return HANDLE_WM_LBUTTONDOWN(hwnd, wParam, lParam, (HWND _, bool _, MouseButtonState _, POINTS pt) => pSample.OnLeftButtonDown((uint)pt.x, (uint)pt.y));

			case WindowMessage.WM_LBUTTONUP:
				return HANDLE_WM_LBUTTONUP(hwnd, wParam, lParam, (HWND _, MouseButtonState _, POINTS pt) => pSample.OnLeftButtonUp((uint)pt.x, (uint)pt.y));

			case WindowMessage.WM_DESTROY:
				break;
		}
		return base.WndProc(hwnd, msg, wParam, lParam);
	}
}