using System.Drawing;
using Vanara.PInvoke;
using static Vanara.PInvoke.D2d1;
using static Vanara.PInvoke.Dwrite;
using static Vanara.PInvoke.DXGI;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.User32;
using static Vanara.PInvoke.WindowsCodecs;

internal class DemoApp : VisibleWindow
{
	private const float sc_boardWidth = 900.0f;
	private const uint sc_defaultNumSquares = 16;
	private const string sc_fontName = "Calibri";
	private const float sc_fontSize = 20.0f;
	private const float sc_loupeInset = 20.0f;
	private const uint sc_maxNumSquares = 1024;

	// This determines that maximum texture size we will generate for our realizations.
	private const uint sc_maxRealizationDimension = 2000;

	private const float sc_maxZoom = 15.0f;
	private const uint sc_minNumSquares = 1;
	private const float sc_minZoom = 1.0f;
	private const float sc_rotationSpeed = 3.0f;
	private const float sc_strokeWidth = 1.0f;
	private const float sc_textInfoBoxInset = 10;
	private const float sc_zoomStep = 1.5f;
	private const float sc_zoomSubStep = 1.1f;
	private static readonly D2D_RECT_F sc_textInfoBox = new(10, 10, 350, 200);
	private readonly RingBuffer<long> m_times = new(10);
	private D2D1_ANTIALIAS_MODE m_antialiasMode = default;
	private bool m_autoGeometryRegen = true;
	private float m_currentZoomFactor = 1f;
	private bool m_drawStroke = true;
	private D2D_POINT_2F m_mousePos = default;
	private uint m_numSquares = sc_defaultNumSquares;
	private bool m_paused = false;
	private long m_pausedTime = 0;
	private ID2D1Factory m_pD2DFactory;
	private IDWriteFactory m_pDWriteFactory;
	private ID2D1Geometry? m_pGeometry = null;
	private IGeometryRealization? m_pRealization = null;
	private ID2D1HwndRenderTarget? m_pRT = null;
	private ID2D1SolidColorBrush? m_pSolidColorBrush = null;
	private IDWriteTextFormat m_pTextFormat;
	private IWICImagingFactory m_pWICFactory;
	private float m_targetZoomFactor = 1f;
	private long m_timeDelta;
	private bool m_updateRealization = true;
	private bool m_useRealizations = false;

	private static class DPIScale
	{
		private static readonly float scale = GetDpiForWindow(GetDesktopWindow()) / 96.0f;
		public static SIZE Scale(int width, int height) => new((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale));
	}

	/******************************************************************
	*                                                                 *
	*  DemoApp constructor                                            *
	*                                                                 *
	*  This method is used to create resources which are not bound 	  *
	*  to any device. Their lifetime effectively extends for the 	  *
	*  duration of the app. These resources include the D2D,		  *
	*  DWrite, and WIC factories; and a DWrite Text Format object 	  *
	*  (used for identifying particular font characteristics) and 	  *
	*  a D2D geometry.												  *
	*                                                                 *
	******************************************************************/
	public DemoApp()
	{
		QueryPerformanceCounter(out var time);
		m_timeDelta = -time;

		//create D2D factory
		m_pD2DFactory = D2D1CreateFactory<ID2D1Factory>();

		// Create a WIC factory.
		m_pWICFactory = new();

		// Create a DirectWrite factory.
		m_pDWriteFactory = DWriteCreateFactory<IDWriteFactory>();

		// Create a DirectWrite text format object.
		m_pTextFormat = m_pDWriteFactory.CreateTextFormat(sc_fontName, default, DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_NORMAL,
			DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_STRETCH.DWRITE_FONT_STRETCH_NORMAL, sc_fontSize, "");
	}

	/******************************************************************
	*                                                                 *
	*  WinMain                                                        *
	*                                                                 *
	*  Application entrypoint                                         *
	*                                                                 *
	******************************************************************/
	public static void Main() => Run<DemoApp>("D2D Demo App", DPIScale.Scale(640, 480));

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
				return FALSE;

			case WindowMessage.WM_PAINT:
			case WindowMessage.WM_DISPLAYCHANGE:
				BeginPaint(hwnd, out var ps);
				OnRender();
				EndPaint(hwnd, ps);
				InvalidateRect(Handle, null, false);
				return FALSE;

			case WindowMessage.WM_KEYDOWN:
				return HANDLE_WM_KEYDOWN(hwnd, wParam, lParam, OnKeyDown);

			case WindowMessage.WM_MOUSEMOVE:
				return HANDLE_WM_MOUSEMOVE(hwnd, wParam, lParam, OnMouseMove);

			case WindowMessage.WM_MOUSEWHEEL:
				return HANDLE_WM_MOUSEWHEEL(hwnd, wParam, lParam, OnWheel);

			default:
				return base.WndProc(hwnd, message, wParam, lParam);
		}
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
	private void CreateDeviceResources()
	{
		if (m_pRT is null)
		{
			GetClientRect(Handle, out var rc);

			D2D_SIZE_U size = (D2D_SIZE_U)rc.Size;

			// Create a Direct2D render target.
			m_pRT = m_pD2DFactory.CreateHwndRenderTarget(RenderTargetProperties(), HwndRenderTargetProperties(Handle, size));

			// Create brushes.
			m_pSolidColorBrush = m_pRT.CreateSolidColorBrush(Color.White);

			IGeometryRealizationFactory pRealizationFactory = new GeometryRealizationFactory(m_pRT, sc_maxRealizationDimension);
			m_pRealization = pRealizationFactory.CreateGeometryRealization();
			m_updateRealization = true;
		}
	}

	private void CreateGeometries()
	{
		if (m_pGeometry is null)
		{
			//IGeometryRealizationFactory pRealizationFactory = default;
			//IGeometryRealization pRealization = default;

			//ID2D1TransformedGeometry pGeometry = default;
			//ID2D1PathGeometry pPathGeometry = default;
			//ID2D1GeometrySink pSink = default;

			float squareWidth = 0.9f * sc_boardWidth / m_numSquares;

			// Create the path geometry.
			var pPathGeometry = m_pD2DFactory.CreatePathGeometry();

			// Write to the path geometry using the geometry sink to create an hour glass shape.
			var pSink = pPathGeometry.Open();

			pSink.SetFillMode(D2D1_FILL_MODE.D2D1_FILL_MODE_ALTERNATE);

			pSink.BeginFigure(Point2F(0, 0), D2D1_FIGURE_BEGIN.D2D1_FIGURE_BEGIN_FILLED);

			pSink.AddLine(Point2F(1.0f, 0));

			pSink.AddBezier(BezierSegment(Point2F(0.75f, 0.25f), Point2F(0.75f, 0.75f), Point2F(1.0f, 1.0f)));

			pSink.AddLine(Point2F(0, 1.0f));

			pSink.AddBezier(BezierSegment(Point2F(0.25f, 0.75f), Point2F(0.25f, 0.25f), Point2F(0, 0)));

			pSink.EndFigure(D2D1_FIGURE_END.D2D1_FIGURE_END_CLOSED);

			pSink.Close().ThrowIfFailed();

			D2D_MATRIX_3X2_F scale = D2D_MATRIX_3X2_F.Scale(squareWidth, squareWidth);
			D2D_MATRIX_3X2_F translation = D2D_MATRIX_3X2_F.Translation(-squareWidth / 2, -squareWidth / 2);

			var pGeometry = m_pD2DFactory.CreateTransformedGeometry(pPathGeometry, scale * translation);

			// Transfer the reference.
			m_pGeometry = pGeometry;
		}
	}

	/******************************************************************
	*                                                                 *
	*  DemoApp::DiscardDeviceResources                                *
	*                                                                 *
	*  Discard device-specific resources which need to be recreated   *
	*  when a Direct3D device is lost.                                *
	*                                                                 *
	******************************************************************/
	private void DiscardDeviceResources()
	{
		m_pRT = null;
		m_pSolidColorBrush = null;
		m_pRealization = null;
	}

	/******************************************************************
	*                                                                 *
	*  DemoApp::DiscardGeometryData                                   *
	*                                                                 *
	******************************************************************/
	private void DiscardGeometryData()
	{
		m_pGeometry = null;
		m_updateRealization = true;
	}

	/******************************************************************
	*                                                                 *
	*  DemoApp::OnKeyDown                                             *
	*                                                                 *
	******************************************************************/
	private void OnKeyDown(HWND hWND, VK vkey, WM_KEY_LPARAM lPARAM)
	{
		switch (vkey)
		{
			case VK.VK_A:
				m_antialiasMode = (m_antialiasMode == D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED) ?
					D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_PER_PRIMITIVE :
					D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED;
				break;

			case VK.VK_R:
				m_useRealizations = !m_useRealizations;
				break;

			case VK.VK_G:
				m_autoGeometryRegen = !m_autoGeometryRegen;
				break;

			case VK.VK_S:
				m_drawStroke = !m_drawStroke;
				break;

			case VK.VK_SPACE:
				QueryPerformanceCounter(out var time);

				if (!m_paused)
				{
					m_pausedTime = time;
				}
				else
				{
					m_timeDelta += (m_pausedTime - time);
				}

				m_paused = !m_paused;
				m_updateRealization = true;

				break;

			case VK.VK_UP:
				m_numSquares = Math.Min(m_numSquares * 2, sc_maxNumSquares);

				// Regenerate the geometries.
				DiscardGeometryData();
				break;

			case VK.VK_DOWN:
				m_numSquares = Math.Max(m_numSquares / 2, sc_minNumSquares);

				// Regenerate the geometries.
				DiscardGeometryData();
				break;

			default:
				break;
		}
	}

	/******************************************************************
	*                                                                 *
	*  OnMouseMove                                                    *
	*                                                                 *
	******************************************************************/
	private void OnMouseMove(HWND hWND, MouseButtonState state, POINTS pt)
	{
		float dpiX = 96.0f;
		float dpiY = 96.0f;

		m_pRT?.GetDpi(out dpiX, out dpiY);

		m_mousePos = Point2F(pt.x * 96.0f / dpiX, pt.y * 96.0f / dpiY);
	}

	/******************************************************************
	*                                                                 *
	*  DemoApp::OnRender                                              *
	*                                                                 *
	*  Called whenever the application needs to display the client    *
	*  window. This method draws the main content (a 2D array of      *
	*  spinning geometries) and some perf statistics.                 *
	*                                                                 *
	*  Note that this function will not render anything if the window *
	*  is occluded (e.g. when the screen is locked).                   *
	*  Also, this function will automatically discard device-specific *
	*  resources if the D3D device disappears during function         *
	*  invocation, and will recreate the resources the next time it's *
	*  invoked.                                                       *
	*                                                                 *
	******************************************************************/
	private void OnRender()
	{
		QueryPerformanceCounter(out var time);
		QueryPerformanceFrequency(out var frequency);

		float floatTime;

		CreateDeviceResources();

		if (!m_paused)
		{
			floatTime = (float)(time + m_timeDelta) / (float)(frequency);
		}
		else
		{
			floatTime = (float)(m_pausedTime + m_timeDelta) / (float)(frequency);
		}

		m_times.Add(time);

		if (m_currentZoomFactor < m_targetZoomFactor)
		{
			m_currentZoomFactor *= sc_zoomSubStep;

			if (m_currentZoomFactor > m_targetZoomFactor)
			{
				m_currentZoomFactor = m_targetZoomFactor;

				if (m_autoGeometryRegen)
				{
					m_updateRealization = true;
				}
			}
		}
		else if (m_currentZoomFactor > m_targetZoomFactor)
		{
			m_currentZoomFactor /= sc_zoomSubStep;

			if (m_currentZoomFactor < m_targetZoomFactor)
			{
				m_currentZoomFactor = m_targetZoomFactor;

				if (m_autoGeometryRegen)
				{
					m_updateRealization = true;
				}
			}
		}

		m_pRT?.SetTransform(D2D_MATRIX_3X2_F.Scale(m_currentZoomFactor, m_currentZoomFactor, m_mousePos.x, m_mousePos.y));

		CreateGeometries();

		if (m_pRT is not null && (m_pRT.CheckWindowState() & D2D1_WINDOW_STATE.D2D1_WINDOW_STATE_OCCLUDED) == 0)
		{
			m_pRT.BeginDraw();

			RenderMainContent(floatTime);

			RenderTextInfo();

			var hr = m_pRT.EndDraw();
			if (hr == HRESULT.D2DERR_RECREATE_TARGET)
				DiscardDeviceResources();
			else
				hr.ThrowIfFailed();
		}
	}

	/******************************************************************
	*                                                                 *
	*  DemoApp::OnResize                                              *
	*                                                                 *
	*  If the application receives a WM_SIZE message, this method     *
	*  resizes the render target appropriately.                       *
	*                                                                 *
	******************************************************************/
	private void OnResize(ushort width, ushort height)
	{
		if (m_pRT is not null)
		{
			D2D_SIZE_U size = new(width, height);

			// Note: This method can fail, but it's okay to ignore the error here -- it will be repeated on the next call to EndDraw.
			m_pRT.Resize(size);
		}
	}

	/******************************************************************
	*                                                                 *
	*  DemoApp::OnWheel                                               *
	*                                                                 *
	******************************************************************/
	private void OnWheel(HWND hWND, MOUSEWHEEL mw, POINTS pt)
	{
		m_targetZoomFactor *= (float)Math.Pow(sc_zoomStep, mw.distance / 120.0f);

		m_targetZoomFactor = Math.Min(Math.Max(m_targetZoomFactor, sc_minZoom), sc_maxZoom);
	}

	private void RenderMainContent(float time)
	{
		m_pRT!.SetAntialiasMode(m_antialiasMode);

		m_pRT.Clear(Color.Black);

		m_pRT.GetTransform(out var currentTransform);

		D2D_SIZE_F rtSize = m_pRT!.GetSize();
		float squareWidth = sc_boardWidth / m_numSquares;
		D2D_MATRIX_3X2_F worldTransform = D2D_MATRIX_3X2_F.Translation(0.5f * (rtSize.width - squareWidth * m_numSquares), 0.5f * (rtSize.height - squareWidth * m_numSquares)) * currentTransform;

		for (uint i = 0; i < m_numSquares; ++i)
		{
			for (uint j = 0; j < m_numSquares; ++j)
			{
				float dx = i + 0.5f - 0.5f * m_numSquares;
				float dy = j + 0.5f - 0.5f * m_numSquares;

				float length = (float)Math.Sqrt(2) * m_numSquares;

				// The intensity variable determines the color and speed of rotation of the realization instance. We choose a function that
				// is rotationaly symmetric about the center of the grid, which produces a nice effect.
				float intensity = 0.5f * (1 + (float)Math.Sin((0.2f * time + 10.0f * Math.Sqrt((float)(dx * dx + dy * dy)) / length)));

				D2D_MATRIX_3X2_F rotateTransform = D2D_MATRIX_3X2_F.Rotation((intensity * sc_rotationSpeed * time * 360.0f) * ((float)Math.PI / 180.0f));

				D2D_MATRIX_3X2_F newWorldTransform = rotateTransform * D2D_MATRIX_3X2_F.Translation((i + 0.5f) * squareWidth, (j + 0.5f) * squareWidth) * worldTransform;

				if (m_updateRealization)
				{
					// Note: It would actually be a little simpler to generate our realizations prior to entering RenderMainContent. We
					// instead generate the realizations based on the top-left primitive in the grid, so we can illustrate the fact that
					// realizations appear identical to their unrealized counter-parts when the exact same world transform is supplied. Only
					// the top left realization will look identical, though, as shifting or rotating an AA realization can introduce fuzziness.
					//
					// Realizations are regenerated every frame, so to demonstrate that the realization geometry produces identical results,
					// you actually need to pause (<space>), which forces a regeneration.
					m_pRealization!.Update(m_pGeometry!,
						REALIZATION_CREATION_OPTIONS.REALIZATION_CREATION_OPTIONS_ANTI_ALIASED |
						REALIZATION_CREATION_OPTIONS.REALIZATION_CREATION_OPTIONS_ALIASED |
						REALIZATION_CREATION_OPTIONS.REALIZATION_CREATION_OPTIONS_FILLED |
						REALIZATION_CREATION_OPTIONS.REALIZATION_CREATION_OPTIONS_STROKED |
						REALIZATION_CREATION_OPTIONS.REALIZATION_CREATION_OPTIONS_UNREALIZED,
						newWorldTransform,
						sc_strokeWidth,
						null); //pIStrokeStyle

					m_updateRealization = false;
				}

				m_pRT.SetTransform(newWorldTransform);

				m_pSolidColorBrush!.SetColor(new(0.0f, intensity, 1.0f - intensity));

				m_pRealization!.Fill(m_pRT, m_pSolidColorBrush,
					m_useRealizations ? REALIZATION_RENDER_MODE.REALIZATION_RENDER_MODE_DEFAULT : REALIZATION_RENDER_MODE.REALIZATION_RENDER_MODE_FORCE_UNREALIZED);

				if (m_drawStroke)
				{
					m_pSolidColorBrush.SetColor(Color.White);

					m_pRealization.Draw(m_pRT, m_pSolidColorBrush,
						m_useRealizations ? REALIZATION_RENDER_MODE.REALIZATION_RENDER_MODE_DEFAULT : REALIZATION_RENDER_MODE.REALIZATION_RENDER_MODE_FORCE_UNREALIZED);
				}
			}
		}
	}

	/******************************************************************
	*                                                                 *
	*  DemoApp::RenderTextInfo                                        *
	*                                                                 *
	*  Draw the stats text (AA type, fps, etc...).                    *
	*                                                                 *
	******************************************************************/
	private void RenderTextInfo()
	{
		QueryPerformanceFrequency(out var frequency);

		uint numPrimitives = m_numSquares * m_numSquares;

		if (m_drawStroke)
		{
			numPrimitives *= 2;
		}

		float fps = 0f, primsPerSecond = 0f;
		if (m_times.Count > 0)
		{
			fps = (m_times.Count - 1) * frequency / (float)((m_times.Last - m_times.First));
			primsPerSecond = fps * numPrimitives;
		}

		var textBuffer = string.Format("{0}\n{1}\n{2}\n# primitives: {3} x {4}{5} = {6}Fps: {7:F2}\nPrimitives / sec : {8:F0}\n",
			m_antialiasMode == D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED ? "Aliased" : "PerPrimitive",
			m_useRealizations ? "Realized" : "Unrealized",
			m_autoGeometryRegen ? "Auto Realization Regeneration" : "No Auto Realization Regeneration",
			m_numSquares,
			m_numSquares,
			m_drawStroke ? " x 2" : "",
			numPrimitives,
			fps,
			primsPerSecond);

		m_pRT!.SetTransform(D2D_MATRIX_3X2_F.Identity());

		m_pSolidColorBrush!.SetColor(new(0.0f, 0.0f, 0.0f, 0.5f));

		m_pRT.FillRoundedRectangle(RoundedRect(sc_textInfoBox,
			sc_textInfoBoxInset,
			sc_textInfoBoxInset),
			m_pSolidColorBrush);

		m_pSolidColorBrush.SetColor(Color.White);

		m_pRT.DrawText(textBuffer,
			(uint)textBuffer.Length,
			m_pTextFormat,
			RectF(sc_textInfoBox.left + sc_textInfoBoxInset,
			sc_textInfoBox.top + sc_textInfoBoxInset,
			sc_textInfoBox.right - sc_textInfoBoxInset,
			sc_textInfoBox.bottom - sc_textInfoBoxInset),
			m_pSolidColorBrush,
			D2D1_DRAW_TEXT_OPTIONS.D2D1_DRAW_TEXT_OPTIONS_NONE);
	}
}