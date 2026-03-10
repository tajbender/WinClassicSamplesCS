using System.Drawing;
using Vanara.PInvoke;
using static Vanara.PInvoke.D2d1;
using static Vanara.PInvoke.DXGI;
using static Vanara.PInvoke.User32;

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

internal class MainWindow : VisibleWindow
{
	private D2D1_ELLIPSE ellipse;
	private ID2D1SolidColorBrush? pBrush;
	private ID2D1Factory pFactory = D2D1CreateFactory<ID2D1Factory>();
	private ID2D1HwndRenderTarget? pRenderTarget;

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
		}
		return base.WndProc(hwnd, msg, wParam, lParam);
	}

	private static void Main() => Run<MainWindow>("Circle");

	// Recalculate drawing layout when the size of the window changes.
	private void CalculateLayout()
	{
		if (pRenderTarget is not null)
		{
			try
			{
				pRenderTarget.GetSize(out var size);
				float x = size.width / 2;
				float y = size.height / 2;
				float radius = Math.Min(x, y);
				ellipse = new(Point2F(x, y), radius, radius);
			}
			catch (Exception ex)
			{
				// Handle exceptions related to layout calculation
				Console.WriteLine($"Error calculating layout: {ex}");
			}
		}
	}

	private HRESULT CreateGraphicsResources()
	{
		HRESULT hr = HRESULT.S_OK;
		if (pRenderTarget is null)
		{
			GetClientRect(Handle, out var rc);

			D2D_SIZE_U size = SizeU((uint)rc.right, (uint)rc.bottom);

			try
			{
				pRenderTarget = pFactory!.CreateHwndRenderTarget(RenderTargetProperties(), HwndRenderTargetProperties(Handle, size));

				D3DCOLORVALUE color = new(1.0f, 1.0f, 0);
				pBrush = pRenderTarget.CreateSolidColorBrush(color);
				CalculateLayout();
			}
			catch (Exception ex)
			{
				hr = ex.HResult;
				DiscardGraphicsResources();
			}
		}
		return hr;
	}

	private void DiscardGraphicsResources()
	{
		pRenderTarget = null;
		pBrush = null;
	}

	private void OnPaint()
	{
		HRESULT hr = CreateGraphicsResources();
		if (hr.Succeeded)
		{
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
	}

	private void Resize()
	{
		if (pRenderTarget is not null)
		{
			GetClientRect(Handle, out var rc);

			var size = SizeU((uint)rc.right, (uint)rc.bottom);
			pRenderTarget.Resize(size);
			CalculateLayout();
			InvalidateRect(Handle, default, false);
		}
	}
}