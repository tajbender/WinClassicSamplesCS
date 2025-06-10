using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using Vanara.Extensions;
using Vanara.PInvoke;
using static Vanara.PInvoke.D2d1;
using static Vanara.PInvoke.DwmApi;
using static Vanara.PInvoke.DXGI;
using static Vanara.PInvoke.Gdi32;
using static Vanara.PInvoke.User32;

internal class DemoApp : VisibleWindow
{
	ID2D1Factory m_pD2DFactory;
	ID2D1PathGeometry m_pPathGeometry;
	ID2D1PathGeometry m_pObjectGeometry;
	ID2D1HwndRenderTarget? m_pRT;
	ID2D1SolidColorBrush? m_pRedBrush;
	ID2D1SolidColorBrush? m_pYellowBrush;

	readonly EaseInOutExponentialAnimation<float> m_Animation = new(0, 0, 0);

	DWM_TIMING_INFO m_DwmTimingInfo;
	float float_time = 0.0f;

	private static class DPIScale
	{
		private static readonly float scale = GetDpiForWindow(GetDesktopWindow()) / 96.0f;
		public static SIZE Scale(int width, int height) => new((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale));
	}

	/******************************************************************
	*                                                                 *
	*  WinMain                                                        *
	*                                                                 *
	*  Application entrypoint                                         *
	*                                                                 *
	******************************************************************/
	public static void Main() => Run<DemoApp>("D2D Simple Path Animation Sample", DPIScale.Scale(640, 480));

	/******************************************************************
	*                                                                 *
	*  DemoApp::DemoApp constructor                                   *
	*                                                                 *
	*  Initialize member data                                         *
	*                                                                 *
	******************************************************************/
	public DemoApp()
	{
		CreateDeviceIndependentResources();

		Created += () =>
		{
			float length = 0;

			m_pPathGeometry!.ComputeLength(default, //no transform
				length);

			m_Animation.Start = 0; //start at beginning of path
			m_Animation.End = length; //length at end of path
			m_Animation.Duration = 5.0f; //seconds

			m_DwmTimingInfo = new() { cbSize = Marshal.SizeOf<DWM_TIMING_INFO>() };

			// Get the composition refresh rate. If the DWM isn't running,
			// get the refresh rate from GDI -- probably going to be 60Hz
			if (DwmGetCompositionTimingInfo(default, ref m_DwmTimingInfo).Failed)
			{
				using var hdc = GetDC(Handle);
				m_DwmTimingInfo.rateCompose.uiDenominator = 1;
				m_DwmTimingInfo.rateCompose.uiNumerator = (uint)GetDeviceCaps(hdc, DeviceCap.VREFRESH);
				ReleaseDC(Handle, hdc);
			}
		};
	}

	/******************************************************************
	*                                                                 *
	*  DemoApp::CreateDeviceIndependentResources                      *
	*                                                                 *
	*  This method is used to create resources which are not bound    *
	*  to any device. Their lifetime effectively extends for the      *
	*  duration of the app.                                           *
	*                                                                 *
	******************************************************************/
	[MemberNotNull(nameof(m_pD2DFactory), nameof(m_pPathGeometry), nameof(m_pObjectGeometry))]
	void CreateDeviceIndependentResources()
	{
		// Create a Direct2D factory.
		m_pD2DFactory = D2D1CreateFactory<ID2D1Factory>();

		// Create the path geometry.
		m_pPathGeometry = m_pD2DFactory.CreatePathGeometry();

		// Write to the path geometry using the geometry sink. We are going to create a
		// spiral
		ID2D1GeometrySink pSink = m_pPathGeometry.Open();

		D2D_POINT_2F currentLocation = default;

		pSink.BeginFigure(currentLocation, D2D1_FIGURE_BEGIN.D2D1_FIGURE_BEGIN_FILLED);

		D2D_POINT_2F locDelta = new(2, 2);
		float radius = 3;

		for (uint i = 0; i < 30; ++i)
		{
			currentLocation.x += radius * locDelta.x;
			currentLocation.y += radius * locDelta.y;

			pSink.AddArc(ArcSegment(currentLocation,
				SizeF(2 * radius, 2 * radius), // radiusx/y
				0.0f, // rotation angle
				D2D1_SWEEP_DIRECTION.D2D1_SWEEP_DIRECTION_CLOCKWISE,
				D2D1_ARC_SIZE.D2D1_ARC_SIZE_SMALL
			));

			locDelta = Point2F(-locDelta.y, locDelta.x);

			radius += 3;
		}

		pSink.EndFigure(D2D1_FIGURE_END.D2D1_FIGURE_END_OPEN);

		pSink.Close();

		// Create the path geometry.
		m_pObjectGeometry = m_pD2DFactory.CreatePathGeometry();

		// Write to the object geometry using the geometry sink.
		// We are going to create a simple triangle
		pSink = m_pObjectGeometry.Open();

		pSink.BeginFigure(Point2F(0.0f, 0.0f), D2D1_FIGURE_BEGIN.D2D1_FIGURE_BEGIN_FILLED);

		D2D_POINT_2F[] ptTriangle = [new(-10.0f, -10.0f), new(-10.0f, 10.0f), new(0.0f, 0.0f)];
		((ID2D1SimplifiedGeometrySink)pSink).AddLines(ptTriangle, 3); // temp bug workaround

		pSink.EndFigure(D2D1_FIGURE_END.D2D1_FIGURE_END_OPEN);

		pSink.Close();
	}

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
		if (m_pRT is null)
		{
			GetClientRect(Handle, out var rc);

			var size = SizeU((uint)rc.right - (uint)rc.left, (uint)rc.bottom - (uint)rc.top);

			// Create a Direct2D render target
			m_pRT = m_pD2DFactory.CreateHwndRenderTarget(RenderTargetProperties(), HwndRenderTargetProperties(Handle, size));

			// Create a red brush.
			m_pRedBrush = m_pRT.CreateSolidColorBrush((D3DCOLORVALUE)Color.Red);

			// Create a yellow brush.
			m_pYellowBrush = m_pRT.CreateSolidColorBrush((D3DCOLORVALUE)Color.Yellow);
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
		m_pRT = null;
		m_pRedBrush = null;
		m_pYellowBrush = null;
	}

	/******************************************************************
	*                                                                 *
	*  DemoApp::OnRender                                              *
	*                                                                 *
	*  Called whenever the application needs to display the client    *
	*  window. This method draws a single frame of animated content   *
	*                                                                 *
	*  Note that this function will not render anything if the window *
	*  is occluded (e.g. when the screen is locked).                  *
	*  Also, this function will automatically discard device-specific *
	*  resources if the D3D device disappears during function         *
	*  invocation, and will recreate the resources the next time it's *
	*  invoked.                                                       *
	*                                                                 *
	******************************************************************/
	void OnRender()
	{
		CreateDeviceResources();
		if (!m_pRT!.CheckWindowState().IsFlagSet(D2D1_WINDOW_STATE.D2D1_WINDOW_STATE_OCCLUDED))
		{
			//D2D1_POINT_2F point;
			//D2D1_POINT_2F tangent;
			//D2D1_MATRIX_3X2_F triangleMatrix;
			var rtSize = m_pRT.GetSize();
			float minWidthHeightScale = Math.Min(rtSize.width, rtSize.height) / 512;

			var scale = D2D_MATRIX_3X2_F.Scale(minWidthHeightScale, minWidthHeightScale);

			var translation = D2D_MATRIX_3X2_F.Translation(rtSize.width / 2, rtSize.height / 2);

			// Prepare to draw.
			m_pRT.BeginDraw();

			// Reset to identity transform
			m_pRT.SetTransform(D2D_MATRIX_3X2_F.Identity());

			//clear the render target contents
			m_pRT.Clear(Color.Black);

			//center the path
			m_pRT.SetTransform(scale * translation);

			//draw the path in red
			m_pRT.DrawGeometry(m_pPathGeometry, m_pRedBrush!);

			float length = m_Animation.GetValue(float_time);

			// Ask the geometry to give us the point that corresponds with the
			// length at the current time.
			m_pPathGeometry.ComputePointAtLength(length, default, default, out var point, out var tangent);

			// Reorient the triangle so that it follows the
			// direction of the path.
			D2D_MATRIX_3X2_F triangleMatrix = new(tangent.x, tangent.y, -tangent.y, tangent.x, point.x, point.y);

			m_pRT.SetTransform(triangleMatrix * scale * translation);

			// Draw the yellow triangle.
			m_pRT.FillGeometry(m_pObjectGeometry, m_pYellowBrush!);

			// Commit the drawing operations.
			var hr = m_pRT.EndDraw();
			if (hr == HRESULT.D2DERR_RECREATE_TARGET)
				DiscardDeviceResources();
			else
				hr.ThrowIfFailed();

			// When we reach the end of the animation, loop back to the beginning.
			if (float_time < m_Animation.Duration)
			{
				float_time += (float)m_DwmTimingInfo.rateCompose.uiDenominator / (float)m_DwmTimingInfo.rateCompose.uiNumerator;
			}
		}

		InvalidateRect(Handle, default, false);
	}

	/******************************************************************
	*                                                                 *
	*  DemoApp::OnResize                                              *
	*                                                                 *
	*  If the application receives a WM_SIZE message, this method     *
	*  resize the render target appropriately.                        *
	*                                                                 *
	******************************************************************/
	void OnResize(uint width, uint height)
	{
		if (m_pRT is not null)
		{
			var size = SizeU(width, height);

			// Note: This method can fail, but it's okay to ignore the
			// error here -- it will be repeated on the next call to
			// EndDraw.
			try { m_pRT.Resize(size); } catch { }
		}
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
			case WindowMessage.WM_SIZE:
				OnResize(Macros.LOWORD(lParam), Macros.HIWORD(lParam));
				return 0;

			case WindowMessage.WM_PAINT:
			case WindowMessage.WM_DISPLAYCHANGE:
				BeginPaint(hwnd, out PAINTSTRUCT ps);
				OnRender();
				EndPaint(hwnd, ps);
				return 0;

			default:
				return base.WndProc(hwnd, message, wParam, lParam);
		}
	}
}
