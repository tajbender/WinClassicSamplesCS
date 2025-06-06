using Vanara.PInvoke;
using static Vanara.PInvoke.User32;

VisibleWindow.Run<MainWindow>("Learn to Program Windows");

class MainWindow : VisibleWindow
{
	protected override nint WndProc(HWND hwnd, uint msg, nint wParam, nint lParam)
	{
		switch ((WindowMessage)msg)
		{
			case WindowMessage.WM_PAINT:
				{
					HDC hdc = BeginPaint(hwnd, out var ps);
					FillRect(hdc, ps.rcPaint, GetSysColorBrush(SystemColorIndex.COLOR_WINDOW));
					EndPaint(hwnd, ps);
				}
				return 0;
		}
		return base.WndProc(hwnd, msg, wParam, lParam);
	}
}