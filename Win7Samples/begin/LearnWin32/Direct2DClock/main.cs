using System.Drawing;
using Vanara.PInvoke;
using static Vanara.PInvoke.D2d1;
using static Vanara.PInvoke.DXGI;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.User32;

internal class MainWindow : VisibleWindow
{
	private readonly Scene m_scene = new();
	private SafeWaitableTimerHandle m_hTimer = SafeWaitableTimerHandle.Null;

	public static void Main()
	{
		using var window = new MainWindow();
		window.CreateHandle(null, "Analog Clock", null, null, WindowStyles.WS_OVERLAPPEDWINDOW | WindowStyles.WS_VISIBLE);
		window.Show();
		new ExaminedMessagePump(null, null, (ref MSG msg) => window.WaitTimer()).Run(window);
	}

	protected override nint WndProc(HWND hwnd, uint uMsg, nint wParam, nint lParam)
	{
		switch ((WindowMessage)uMsg)
		{
			case WindowMessage.WM_CREATE:
				InitializeTimer();
				return 0;

			case WindowMessage.WM_DESTROY:
				CloseHandle(m_hTimer);
				m_scene.CleanUp();
				return 0;

			case WindowMessage.WM_PAINT:
			case WindowMessage.WM_DISPLAYCHANGE:
				{
					BeginPaint(Handle, out var ps);
					m_scene.Render(Handle);
					EndPaint(Handle, ps);
				}
				return 0;

			case WindowMessage.WM_SIZE:
				{
					SIZES pts = (SIZES)lParam;
					m_scene.Resize(pts.Width, pts.Height);
					InvalidateRect(Handle, default, false);
				}
				return 0;

			case WindowMessage.WM_ERASEBKGND:
				return 1;

			default:
				return base.WndProc(hwnd, uMsg, wParam, lParam);
		}
	}

	private bool InitializeTimer()
	{
		m_hTimer = CreateWaitableTimer(default, false, default);
		if (m_hTimer.IsInvalid)
			return false;

		if (!SetWaitableTimer(m_hTimer, 0, (1000 / 60)))
		{
			m_hTimer = SafeWaitableTimerHandle.Null;
			return false;
		}

		return true;
	}

	private void WaitTimer()
	{
		// Wait until the timer expires or any message is posted.
		if (MsgWaitForMultipleObjects(1, [m_hTimer], false, INFINITE, QS.QS_ALLINPUT)
			== (uint)WAIT_STATUS.WAIT_OBJECT_0)
		{
			InvalidateRect(Handle, default, false);
		}
	}
}

internal class Scene : GraphicsScene
{
	private readonly D2D_POINT_2F[] m_Ticks = new D2D_POINT_2F[24];
	private D2D1_ELLIPSE m_ellipse;
	private ID2D1SolidColorBrush? m_pFill;
	private ID2D1SolidColorBrush? m_pStroke;

	protected override void CalculateLayout()
	{
		m_pRenderTarget!.GetSize(out var fSize);

		float x = fSize.width / 2.0f;
		float y = fSize.height / 2.0f;
		float radius = Math.Min(x, y);

		m_ellipse = Ellipse(Point2F(x, y), radius, radius);

		// Calculate tick marks.

		D2D_POINT_2F pt1 = Point2F(m_ellipse.point.x,
		m_ellipse.point.y - (m_ellipse.radiusY * 0.9f));

		D2D_POINT_2F pt2 = Point2F(m_ellipse.point.x,
		m_ellipse.point.y - m_ellipse.radiusY);

		for (uint i = 0; i < 12; i++)
		{
			D2D1MakeRotateMatrix((360.0f / 12) * i, m_ellipse.point, out var mat);

			m_Ticks[i * 2] = mat.TransformPoint(pt1);
			m_Ticks[i * 2 + 1] = mat.TransformPoint(pt2);
		}
	}

	protected override HRESULT CreateDeviceDependentResources()
	{
		try
		{
			var bp = BrushProperties();
			m_pFill = m_pRenderTarget!.CreateSolidColorBrush(new D3DCOLORVALUE(1.0f, 1.0f, 0), bp);
			m_pStroke = m_pRenderTarget!.CreateSolidColorBrush(Color.Black, bp);
			return HRESULT.S_OK;
		}
		catch (Exception ex)
		{
			return ex.HResult;
		}
	}

	protected override void DiscardDeviceDependentResources()
	{
		m_pFill = null;
		m_pStroke = null;
	}

	protected override void RenderScene()
	{
		m_pRenderTarget!.Clear((D3DCOLORVALUE)Color.SkyBlue);

		m_pRenderTarget!.FillEllipse(m_ellipse, m_pFill!);
		m_pRenderTarget.DrawEllipse(m_ellipse, m_pStroke!);

		// Draw tick marks
		for (uint i = 0; i < 12; i++)
		{
			m_pRenderTarget.DrawLine(m_Ticks[i * 2], m_Ticks[i * 2 + 1], m_pStroke!, 2.0f);
		}

		// Draw hands
		GetLocalTime(out var time);

		// 60 minutes = 30 degrees, 1 minute.5 degree
		float fHourAngle = (360.0f / 12) * (time.wHour) + (time.wMinute * 0.5f);
		float fMinuteAngle = (360.0f / 60) * (time.wMinute);
		float fSecondAngle = (360.0f / 60) * (time.wSecond) + (360.0f / 60000) * (time.wMilliseconds);

		DrawClockHand(0.6f, fHourAngle, 6);
		DrawClockHand(0.85f, fMinuteAngle, 4);
		DrawClockHand(0.85f, fSecondAngle, 1);

		// Restore the identity transformation.
		m_pRenderTarget.SetTransform(D2D_MATRIX_3X2_F.Identity());
	}

	private void DrawClockHand(float fHandLength, float fAngle, float fStrokeWidth)
	{
		D2D1MakeRotateMatrix(fAngle, m_ellipse.point, out var matrix);
		m_pRenderTarget!.SetTransform(matrix);

		// endPoint defines one end of the hand.
		D2D_POINT_2F endPoint = Point2F(m_ellipse.point.x,
			m_ellipse.point.y - (m_ellipse.radiusY * fHandLength));

		// Draw a line from the center of the ellipse to endPoint.
		m_pRenderTarget.DrawLine(m_ellipse.point, endPoint, m_pStroke!, fStrokeWidth);
	}
}