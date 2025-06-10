using System.Drawing;
using Vanara.PInvoke;
using static Vanara.PInvoke.D2d1;
using static Vanara.PInvoke.DXGI;
using static Vanara.PInvoke.Gdi32;
using static Vanara.PInvoke.User32;

internal class DemoApp : VisibleWindow
{
	readonly ID2D1Factory m_pD2DFactory;
	ID2D1DCRenderTarget? m_pDCRT;
	ID2D1SolidColorBrush? m_pBlackBrush;

	private static class DPIScale
	{
		private static float scale = GetDpiForWindow(GetDesktopWindow()) / 96.0f;
		public static SIZE Scale(int width, int height) => new((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale));
	}

	public static void Main() => Run<DemoApp>("Direct2D Demo App", DPIScale.Scale(640, 480));

	/******************************************************************
	*                                                                 *
	*  DemoApp::DemoApp constructor                                   *
	*                                                                 *
	*  Initialize member data                                         *
	*                                                                 *
	******************************************************************/
	public DemoApp() =>
		// Create D2D factory
		m_pD2DFactory = D2D1CreateFactory<ID2D1Factory>();

	/******************************************************************
	*                                                                 *
	*  DemoApp::CreateDeviceResources                                 *
	*                                                                 *
	*  This method creates resources which are bound to a particular  *
	*  D3D device. It's all centralized here, in case the resources   *
	*  need to be recreated in case of D3D device loss (eg. display   *
	*  change, remoting, removal of video card, etc).                 *
	*                                                                 *
	******************************************************************/
	void CreateDeviceResources()
	{
		if (m_pDCRT is null)
		{
			// Create a DC render target.
			D2D1_RENDER_TARGET_PROPERTIES props = RenderTargetProperties(D2D1_RENDER_TARGET_TYPE.D2D1_RENDER_TARGET_TYPE_DEFAULT,
				PixelFormat(DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_IGNORE), 0, 0,
				D2D1_RENDER_TARGET_USAGE.D2D1_RENDER_TARGET_USAGE_NONE, D2D1_FEATURE_LEVEL.D2D1_FEATURE_LEVEL_DEFAULT);

			m_pDCRT = m_pD2DFactory.CreateDCRenderTarget(props);

			// Create a black brush.
			m_pBlackBrush = m_pDCRT.CreateSolidColorBrush(new(Color.Black));
		}
	}

	/******************************************************************
	*                                                                 *
	*  DemoApp::DiscardDeviceResources                                *
	*                                                                 *
	*  Discard device-specific resources which need to be recreated   *
	*  when a D3D device is lost                                      *
	*                                                                 *
	******************************************************************/
	void DiscardDeviceResources()
	{
		m_pDCRT = null;
		m_pBlackBrush = null;
	}

	/******************************************************************
	*                                                                 *
	*  DemoApp::OnRender                                              *
	*                                                                 *
	*  This method draws Direct2D content to a GDI HDC.               *
	*                                                                 *
	*  This method will automatically discard device-specific         *
	*  resources if the D3D device disappears during function         *
	*  invocation, and will recreate the resources the next time it's *
	*  invoked.                                                       *
	*                                                                 *
	******************************************************************/
	void OnRender(in PAINTSTRUCT ps)
	{
		// Get the dimensions of the client drawing area.
		GetClientRect(Handle, out var rc);

		//
		// Draw the pie chart with Direct2D.
		//

		// Create the DC render target.
		CreateDeviceResources();

		// Bind the DC to the DC render target.
		m_pDCRT!.BindDC(ps.hdc, rc);

		m_pDCRT.BeginDraw();

		m_pDCRT.SetTransform(D2D_MATRIX_3X2_F.Identity());

		m_pDCRT.Clear(Color.White);

		m_pDCRT.DrawEllipse(Ellipse(Point2F(150.0f, 150.0f), 100.0f, 100.0f), m_pBlackBrush!, 3.0f);

		m_pDCRT.DrawLine(Point2F(150.0f, 150.0f), Point2F((150.0f + 100.0f * 0.15425f), (150.0f - 100.0f * 0.988f)),
			m_pBlackBrush!, 3.0f);

		m_pDCRT.DrawLine(Point2F(150.0f, 150.0f), Point2F((150.0f + 100.0f * 0.525f), (150.0f + 100.0f * 0.8509f)), 
			m_pBlackBrush!, 3.0f);

		m_pDCRT.DrawLine(Point2F(150.0f, 150.0f), Point2F((150.0f - 100.0f * 0.988f), (150.0f - 100.0f * 0.15425f)),
			m_pBlackBrush!, 3.0f);

		var hr = m_pDCRT.EndDraw();
		if (hr.Succeeded)
		{
			//
			// Draw the pie chart with GDI.
			//

			// Save the original object.
			var original = SelectObject(ps.hdc, GetStockObject(StockObjectType.DC_PEN));

			using var blackPen = CreatePen(PenStyle.PS_SOLID, 3, 0);
			SelectObject(ps.hdc, blackPen);

			Ellipse(ps.hdc, 300, 50, 500, 250);

			POINT[] pntArray1 = [new(400, 150), new((int)(400 + 100 * 0.15425), (int)(150 - 100 * 0.9885))];
			POINT[] pntArray2 = [new(400, 150), new((int)(400 + 100 * 0.525), (int)(150 - 100 * 0.8509))];
			POINT[] pntArray3 = [new(400, 150), new((int)(400 + 100 * 0.988), (int)(150 - 100 * 0.15425))];

			Polyline(ps.hdc, pntArray1, 2);
			Polyline(ps.hdc, pntArray2, 2);
			Polyline(ps.hdc, pntArray3, 2);

			DeleteObject(blackPen);

			// Restore the original object.
			SelectObject(ps.hdc, original);
		}

		if (hr == HRESULT.D2DERR_RECREATE_TARGET)
		{
			DiscardDeviceResources();
		}
		else
			hr.ThrowIfFailed();
	}


	/******************************************************************
	*                                                                 *
	*  DemoApp::WndProc                                               *
	*                                                                 *
	*  Window message handler                                         *
	*                                                                 *
	******************************************************************/
	protected override nint WndProc(HWND hwnd, uint message, nint wParam, nint lParam)
	{
		switch ((WindowMessage)message)
		{
			case WindowMessage.WM_PAINT:
			case WindowMessage.WM_DISPLAYCHANGE:
				BeginPaint(hwnd, out PAINTSTRUCT ps);
				OnRender(ps);
				EndPaint(hwnd, ps);
				return 0;

			default:
				return base.WndProc(hwnd, message, wParam, lParam);
		}
	}
}