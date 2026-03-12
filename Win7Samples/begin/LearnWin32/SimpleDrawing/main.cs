using System.Drawing;
using Vanara.PInvoke;
using static Vanara.PInvoke.Macros;
using static Vanara.PInvoke.D2d1;
using static Vanara.PInvoke.DXGI;
using static Vanara.PInvoke.User32;
using Vanara.Extensions;

internal class MainWindow : VisibleWindow
{
	const ushort ID_TOGGLE_MODE = 40002, ID_DRAW_MODE = 40003, ID_SELECT_MODE = 40004;

	private static void Main()
	{
		Accelerator[] accelerators = [new(ID_TOGGLE_MODE, VK.VK_M, ConsoleModifiers.Control), new(ID_DRAW_MODE, VK.VK_F1), new(ID_SELECT_MODE, VK.VK_F2)];
		using var hAccel = accelerators.CreateHandle();
		Run<MainWindow>("Draw Circles", hAccl: hAccel);
	}

	private readonly D3DCOLORVALUE[] colors = [(D3DCOLORVALUE)(COLORREF)Color.Yellow, (D3DCOLORVALUE)(COLORREF)Color.Salmon, (D3DCOLORVALUE)(COLORREF)Color.LimeGreen];
	private readonly List<MyEllipse> ellipses = [];
	private SafeHCURSOR hCursor = SafeHCURSOR.Null;
	private Mode mode;
	private SizeT nextColor;
	private ID2D1SolidColorBrush? pBrush = null;
	private ID2D1Factory? pFactory = null;
	private ID2D1HwndRenderTarget? pRenderTarget = null;
	private D2D_POINT_2F ptMouse;

	private enum Mode
	{
		DrawMode,
		SelectMode,
		DragMode
	}

	protected override nint WndProc(HWND hwnd, uint uMsg, nint wParam, nint lParam)
	{
		switch ((WindowMessage)uMsg)
		{
			case WindowMessage.WM_CREATE:
				if (D2D1CreateFactory(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED, out pFactory).Failed)
				{
					return -1;
				}
				DPIScale.Initialize(hwnd!);
				SetMode(Mode.DrawMode);
				return 0;

			case WindowMessage.WM_DESTROY:
				DiscardGraphicsResources();
				pFactory = null;
				PostQuitMessage(0);
				return 0;

			case WindowMessage.WM_PAINT:
				OnPaint();
				return 0;

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

			case WindowMessage.WM_SETCURSOR:
				if ((HitTestValues)LOWORD(lParam) == HitTestValues.HTCLIENT)
				{
					SetCursor(hCursor);
					return 1;
				}
				break;

			case WindowMessage.WM_KEYDOWN:
				OnKeyDown((VK)(int)wParam);
				return 0;

			case WindowMessage.WM_COMMAND:
				switch (LOWORD(wParam))
				{
					case ID_DRAW_MODE:
						SetMode(Mode.DrawMode);
						break;

					case ID_SELECT_MODE:
						SetMode(Mode.SelectMode);
						break;

					case ID_TOGGLE_MODE:
						SetMode(mode == Mode.DrawMode ? Mode.SelectMode : Mode.DrawMode);
						break;
				}
				return 0;
		}
		return base.WndProc(hwnd, uMsg, wParam, lParam);
	}

	private void ClearSelection() => Selection = null;

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

	private bool HitTest(float x, float y)
	{
		foreach (var e in ellipses)
		{
			if (e.HitTest(x, y))
			{
				Selection = e;
				return true;
			}
		}
		return false;
	}

	private HRESULT InsertEllipse(float x, float y)
	{
		try
		{
			Selection = new(colors[nextColor], new(ptMouse = new(x, y), 2f, 2f));
			ellipses.Add(Selection!);

			nextColor = (nextColor + 1) % colors.Length;
		}
		catch
		{
			return HRESULT.E_OUTOFMEMORY;
		}
		return HRESULT.S_OK;
	}

	private void MoveSelection(float x, float y)
	{
		if ((mode == Mode.SelectMode) && Selection is not null)
		{
			Selection.ellipse.point.x += x;
			Selection.ellipse.point.y += y;
			InvalidateRect(Handle, default, false);
		}
	}

	private void OnKeyDown(VK vkey)
	{
		switch (vkey)
		{
			case VK.VK_BACK:
			case VK.VK_DELETE:
				if ((mode == Mode.SelectMode) && Selection is not null)
				{
					ellipses.Remove(Selection);
					ClearSelection();
					SetMode(Mode.SelectMode);
					InvalidateRect(Handle, default, false);
				}
				;
				break;

			case VK.VK_LEFT:
				MoveSelection(-1, 0);
				break;

			case VK.VK_RIGHT:
				MoveSelection(1, 0);
				break;

			case VK.VK_UP:
				MoveSelection(0, -1);
				break;

			case VK.VK_DOWN:
				MoveSelection(0, 1);
				break;
		}
	}

	private void OnLButtonDown(POINTS pixel, MouseButtonState flags)
	{
		float dipX = DPIScale.PixelsToDipsX(pixel.x);
		float dipY = DPIScale.PixelsToDipsY(pixel.y);

		if (mode == Mode.DrawMode)
		{
			POINT pt = pixel;
			if (DragDetect(Handle, pt))
			{
				SetCapture(Handle);

				// Start a new ellipse.
				InsertEllipse(dipX, dipY);
			}
		}
		else
		{
			ClearSelection();

			if (HitTest(dipX, dipY))
			{
				SetCapture(Handle);

				ptMouse = Selection!.ellipse.point;
				ptMouse.x -= dipX;
				ptMouse.y -= dipY;

				SetMode(Mode.DragMode);
			}
		}
		InvalidateRect(Handle, default, false);
	}

	private void OnLButtonUp()
	{
		if ((mode == Mode.DrawMode) && Selection is not null)
		{
			ClearSelection();
			InvalidateRect(Handle, default, false);
		}
		else if (mode == Mode.DragMode)
		{
			SetMode(Mode.SelectMode);
		}
		ReleaseCapture();
	}

	private void OnMouseMove(POINTS pixel, MouseButtonState flags)
	{
		float dipX = DPIScale.PixelsToDipsX(pixel.x);
		float dipY = DPIScale.PixelsToDipsY(pixel.y);

		if (flags.IsFlagSet(MouseButtonState.MK_LBUTTON) && Selection is not null)
		{
			if (mode == Mode.DrawMode)
			{
				// Resize the ellipse.
				float width = (dipX - ptMouse.x) / 2;
				float height = (dipY - ptMouse.y) / 2;
				float x1 = ptMouse.x + width;
				float y1 = ptMouse.y + height;

				Selection.ellipse = Ellipse(Point2F(x1, y1), width, height);
			}
			else if (mode == Mode.DragMode)
			{
				// Move the ellipse.
				Selection.ellipse.point.x = dipX + ptMouse.x;
				Selection.ellipse.point.y = dipY + ptMouse.y;
			}
			InvalidateRect(Handle, default, false);
		}
	}

	private void OnPaint()
	{
		HRESULT hr = CreateGraphicsResources();
		if (hr.Succeeded)
			try
			{
				BeginPaint(Handle, out var ps);

				pRenderTarget!.BeginDraw();

				pRenderTarget.Clear(Color.SkyBlue);

				foreach (var e in ellipses)
				{
					e.Draw(pRenderTarget, pBrush!);
				}

				if (Selection is not null)
				{
					pBrush!.SetColor(new(Color.Red));
					pRenderTarget.DrawEllipse(Selection.ellipse, pBrush, 2.0f);
				}

				pRenderTarget.EndDraw(out _, out _);
				EndPaint(Handle, ps);
			}
			catch
			{
				DiscardGraphicsResources();
			}
	}

	private void Resize()
	{
		if (pRenderTarget is not null)
		{
			GetClientRect(Handle, out var rc);

			D2D_SIZE_U size = new((uint)rc.right, (uint)rc.bottom);

			pRenderTarget.Resize(size);

			InvalidateRect(Handle, default, false);
		}
	}

	private MyEllipse? Selection { get; set; }

	private void SetMode(Mode m)
	{
		var cursor = (mode = m) switch
		{
			Mode.DrawMode => IDC_CROSS,
			Mode.SelectMode => IDC_HAND,
			Mode.DragMode => IDC_SIZEALL,
			_ => IDC_ARROW,
		};
		hCursor = LoadCursor(default, cursor);
		SetCursor(hCursor);
	}

	private class MyEllipse(D3DCOLORVALUE c, D2D1_ELLIPSE e)
	{
		public D3DCOLORVALUE color = c;
		public D2D1_ELLIPSE ellipse = e;

		public void Draw(ID2D1RenderTarget pRT, ID2D1SolidColorBrush pBrush)
		{
			pBrush.SetColor(color);
			pRT.FillEllipse(ellipse, pBrush);
			pBrush.SetColor((D3DCOLORVALUE)(COLORREF)Color.Black);
			pRT.DrawEllipse(ellipse, pBrush, 1.0f);
		}

		public bool HitTest(float x, float y)
		{
			float a = ellipse.radiusX;
			float b = ellipse.radiusY;
			float x1 = x - ellipse.point.x;
			float y1 = y - ellipse.point.y;
			float d = ((x1 * x1) / (a * a)) + ((y1 * y1) / (b * b));
			return d <= 1.0f;
		}
	}

	private static class DPIScale
	{
		private static float scaleX = 1f, scaleY = 1f;

		public static void Initialize(HWND h)
		{
			var dpi = GetDpiForWindow(h);
			scaleX = scaleY = dpi / 96.0f;
		}

		public static float PixelsToDipsX<T>(T x) where T : struct, IConvertible => x.ToSingle(null) / scaleX;

		public static float PixelsToDipsY<T>(T y) where T : struct, IConvertible => y.ToSingle(null) / scaleY;
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