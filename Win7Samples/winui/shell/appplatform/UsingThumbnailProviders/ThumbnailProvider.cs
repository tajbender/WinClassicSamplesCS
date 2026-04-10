using System.Drawing;
using Vanara.PInvoke;
using static Vanara.PInvoke.Gdi32;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.Shell32;
using static Vanara.PInvoke.User32;

Thread.CurrentThread.SetApartmentState(ApartmentState.Unknown);
Thread.CurrentThread.SetApartmentState(ApartmentState.STA);

SafeHBITMAP? g_hThumbnail; // Thumbnail to create

if (args.Length == 2)
{
	string pszFile = args[1];
	uint nSize = uint.Parse(args[0]); // Size of thumbnail

	IShellItem? psi = SHCreateItemFromParsingName<IShellItem>(pszFile);
	if (psi is not null)
	{
		IThumbnailProvider pThumbProvider = psi.BindToHandler<IThumbnailProvider>(default, BHID.BHID_ThumbnailHandler);
		pThumbProvider.GetThumbnail(nSize, out var hThumbnail, out _).ThrowIfFailed();
		g_hThumbnail = new(hThumbnail, true);

		using var g_hInstance = GetModuleHandle();
		WindowClass wc = new("ThumbnailAppClass", g_hInstance, WndProc, hCursor: LoadCursor(default, IDC_ARROW), hbrBkgd: GetSysColorBrush(SystemColorIndex.COLOR_WINDOW + 1));
		VisibleWindow.Run(WndProc, "Thumbnail Provider SDK Sample");
		g_hThumbnail.Dispose();
	}
}
else
{
	MessageBox(default, "Usage: ThumbnailProvider.exe <size> <Absolute Path to file>", "Wrong number of arguments.", MB_FLAGS.MB_OK);
}

IntPtr WndProc(HWND hWnd, uint message, IntPtr wParam, IntPtr lParam)
{
	switch ((WindowMessage)message)
	{
		case WindowMessage.WM_PAINT:
			HDC hdc = BeginPaint(hWnd, out var ps);

			using (Graphics pGraphics = Graphics.FromHdc(hdc.DangerousGetHandle()))
			using (Bitmap pBitmap = Bitmap.FromHbitmap(g_hThumbnail?.DangerousGetHandle() ?? default, default))
				pGraphics.DrawImage(pBitmap, 0, 0);

			EndPaint(hWnd, ps);
			break;

		case WindowMessage.WM_DESTROY:
			PostQuitMessage(0);
			break;

		default:
			return DefWindowProc(hWnd, message, wParam, lParam);
	}
	return default;
}