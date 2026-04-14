//#define VANARAEXT
using Vanara.PInvoke;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.User32;

#if VANARAEXT
VisibleWindow.Run(WindowProc, "Learn to Program Windows");
#else
SafeHINSTANCE hInstance = GetModuleHandle(null);

const string CLASS_NAME = "Sample Window Class";

// Register the window class.
WNDCLASS wc = new();
wc.lpfnWndProc = WindowProc;
wc.hInstance = hInstance;
wc.lpszClassName = CLASS_NAME;

RegisterClass(wc);

// Create the window.
SafeHWND hwnd = CreateWindowEx(0, // Optional window styles.
	CLASS_NAME, // Window class
	"Learn to Program Windows", // Window text
	WindowStyles.WS_OVERLAPPEDWINDOW, // Window style

	// Size and position
	CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT,

	default, // Parent window 
	default, // Menu
	hInstance, // Instance handle
	default); // Additional application data

if (hwnd.IsInvalid)
	return 0;

ShowWindow(hwnd, ShowWindowCommand.SW_NORMAL);

// Run the message loop.
MSG msg = default;
while (GetMessage(out msg, default, 0, 0) != 0)
{
	TranslateMessage(msg);
	DispatchMessage(msg);
}
#endif

return 0;

static IntPtr WindowProc(HWND hwnd, uint uMsg, IntPtr wParam, IntPtr lParam)
{
	switch ((WindowMessage)uMsg)
	{
		case WindowMessage.WM_DESTROY:
			PostQuitMessage(0);
			return 0;

		case WindowMessage.WM_PAINT:
			{
				HDC hdc = BeginPaint(hwnd, out var ps);

				// All painting occurs here, between BeginPaint and EndPaint.
				FillRect(hdc, ps.rcPaint, GetSysColorBrush(SystemColorIndex.COLOR_WINDOW));
				EndPaint(hwnd, ps);
			}
			return 0;
	}

	return DefWindowProc(hwnd, uMsg, wParam, lParam);
}