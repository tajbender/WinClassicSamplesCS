using System.Drawing;
using Vanara.PInvoke;
using static Vanara.PInvoke.D2d1;
using static Vanara.PInvoke.DXGI;
using static Vanara.PInvoke.User32;
using Vanara.Extensions;

internal class MainWindow : VisibleWindow
{
	private D2D1_ELLIPSE ellipse;
	private ID2D1SolidColorBrush? pBrush;
	private ID2D1Factory pFactory = D2D1CreateFactory<ID2D1Factory>();
	private ID2D1HwndRenderTarget? pRenderTarget;
	private D2D_POINT_2F ptMouse;

	private static void Main() => Run<MainWindow>("Circle");

	private void CalculateLayout() { }

	private HRESULT CreateGraphicsResources()
	{
		if (pRenderTarget is null)
		{
			try
			{
				GetClientRect(Handle, out var rc);

				D2D_SIZE_U size = new((uint)rc.right, (uint)rc.bottom);
				pRenderTarget = pFactory!.CreateHwndRenderTarget(RenderTargetProperties(), HwndRenderTargetProperties(Handle, size));

				D3DCOLORVALUE color = new(1.0f, 1.0f, 0);
				pBrush = pRenderTarget!.CreateSolidColorBrush(color);

				CalculateLayout();
			}
			catch (Exception ex)
			{
				pRenderTarget = null;
				pBrush = null;
				return ex.HResult;
			}
		}
		return HRESULT.S_OK;
	}

	private void DiscardGraphicsResources()
	{
		pRenderTarget = null;
		pBrush = null;
	}

	void OnPaint()
	{
		HRESULT hr = CreateGraphicsResources();
		if (hr.Succeeded)
			try
			{
				BeginPaint(Handle, out var ps);

				pRenderTarget!.BeginDraw();

				pRenderTarget.Clear(Color.SkyBlue);

				pRenderTarget.FillEllipse(ellipse, pBrush!);

				pRenderTarget.EndDraw(out _, out _);
				EndPaint(Handle, ps);
			}
			catch
			{
				DiscardGraphicsResources();
			}
	}

	void Resize()
	{
		if (pRenderTarget is not null)
		{
			GetClientRect(Handle, out var rc);

			D2D_SIZE_U size = new((uint)rc.right, (uint)rc.bottom);

			pRenderTarget.Resize(size);

			CalculateLayout();
			InvalidateRect(Handle, default, false);
		}
	}

	void OnLButtonDown(POINTS pixel, MouseButtonState flags)
	{
		SetCapture(Handle);
		ellipse.point = ptMouse = DPIScale.PixelsToDips(pixel.x, pixel.y);
		ellipse.radiusX = ellipse.radiusY = 1.0f;
		InvalidateRect(Handle, default, false);
	}

	void OnMouseMove(POINTS pixel, MouseButtonState flags)
	{
		if (flags.IsFlagSet(MouseButtonState.MK_LBUTTON))
		{
			D2D_POINT_2F dips = DPIScale.PixelsToDips(pixel.x, pixel.y);

			float width = (dips.x - ptMouse.x) / 2;
			float height = (dips.y - ptMouse.y) / 2;
			float x1 = ptMouse.x + width;
			float y1 = ptMouse.y + height;

			ellipse = Ellipse(Point2F(x1, y1), width, height);

			InvalidateRect(Handle, default, false);
		}
	}

	void OnLButtonUp()
	{
		ReleaseCapture();
	}

	protected override nint WndProc(HWND hwnd, uint msg, nint wParam, nint lParam)
	{
		switch ((WindowMessage)msg)
		{
			case WindowMessage.WM_DESTROY:
				DiscardGraphicsResources();
				return 0;

			case WindowMessage.WM_PAINT:
				OnPaint();
				return 0;

			// Other messages not shown...

			case WindowMessage.WM_SIZE:
				Resize();
				return 0;

			case WindowMessage.WM_LBUTTONDOWN:
				OnLButtonDown((POINTS)lParam, (MouseButtonState)wParam);
				return 0;

			case WindowMessage.WM_LBUTTONUP:
				OnLButtonUp();
				return 0;

			case WindowMessage.WM_MOUSEMOVE:
				OnMouseMove((POINTS)lParam, (MouseButtonState)wParam);
				return 0;
		}
		return DefWindowProc(hwnd, msg, wParam, lParam);
	}

	private static class DPIScale
	{
		private static float scaleX = 1f, scaleY = 1f;

		public static void Initialize(HWND h)
		{
			var dpi = GetDpiForWindow(h);
			scaleX = scaleY = dpi / 96.0f;
		}

		public static D2D_POINT_2F PixelsToDips<T>(T x, T y) where T : struct, IConvertible =>
			new(x.ToSingle(null) / scaleX, y.ToSingle(null) / scaleY);
	}
}

internal static class Ext
{
	public static void Clear(this ID2D1HwndRenderTarget t, Color c)
	{
		unsafe
		{
			D3DCOLORVALUE cv = new(c);
			t.Clear(cv);
		}
	}
}