using System.Drawing;
using Vanara.Extensions;
using Vanara.PInvoke;
using static Vanara.PInvoke.D2d1;
using static Vanara.PInvoke.Dwrite;
using static Vanara.PInvoke.DXGI;
using static Vanara.PInvoke.User32;

internal class DemoApp : VisibleWindow
{
	private readonly ID2D1Factory m_pD2DFactory;
	private readonly IDWriteFactory m_pDWriteFactory;
	private readonly IDWriteTextFormat m_pTextFormat;
	private ID2D1SolidColorBrush? m_pBlackBrush;
	private ID2D1HwndRenderTarget? m_pRenderTarget;

	// Creates the application window and initializes device-independent resources.
	public DemoApp()
	{
		const string msc_fontName = "Verdana";
		const float msc_fontSize = 50f;

		// Create a Direct2D factory.
		m_pD2DFactory = D2D1CreateFactory<ID2D1Factory>();

		// Create a DirectWrite factory.
		m_pDWriteFactory = DWriteCreateFactory<IDWriteFactory>();

		// Create a DirectWrite text format object.
		m_pTextFormat = m_pDWriteFactory.CreateTextFormat(msc_fontName, default, DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_NORMAL,
			DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_STRETCH.DWRITE_FONT_STRETCH_NORMAL, msc_fontSize, "");

		// Center the text horizontally and vertically.
		m_pTextFormat.SetTextAlignment(DWRITE_TEXT_ALIGNMENT.DWRITE_TEXT_ALIGNMENT_CENTER);
		m_pTextFormat.SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT.DWRITE_PARAGRAPH_ALIGNMENT_CENTER);
	}

	// Provides the entry point to the application.
	public static void Main() => Run<DemoApp>("Direct2D Hello World");

	// The window message handler.
	protected override nint WndProc(HWND hwnd, uint message, nint wParam, nint lParam)
	{
		switch ((WindowMessage)message)
		{
			case WindowMessage.WM_SIZE:
				{
					uint width = Macros.LOWORD(lParam);
					uint height = Macros.HIWORD(lParam);
					OnResize(width, height);
				}
				return FALSE;

			case WindowMessage.WM_PAINT:
			case WindowMessage.WM_DISPLAYCHANGE:
				{
					BeginPaint(hwnd, out var ps);
					OnRender();
					EndPaint(hwnd, ps);
				}
				return FALSE;

			default:
				return base.WndProc(hwnd, message, wParam, lParam);
		}
	}

	// This method creates resources which are bound to a particular Direct3D device. It's all centralized here, in case the resources need
	// to be recreated in case of Direct3D device loss (eg. display change, remoting, removal of video card, etc).
	private void CreateDeviceResources()
	{
		if (m_pRenderTarget is null)
		{
			GetClientRect(Handle, out var rc);

			D2D_SIZE_U size = SizeU((uint)rc.right - (uint)rc.left, (uint)rc.bottom - (uint)rc.top);

			// Create a Direct2D render target.
			m_pRenderTarget = m_pD2DFactory.CreateHwndRenderTarget(RenderTargetProperties(), HwndRenderTargetProperties(Handle, size));

			// Create a black brush.
			m_pBlackBrush = m_pRenderTarget.CreateSolidColorBrush((D3DCOLORVALUE)Color.Black);
		}
	}

	// Discard device-specific resources which need to be recreated when a Direct3D device is lost
	private void DiscardDeviceResources()
	{
		m_pRenderTarget = null;
		m_pBlackBrush = null;
	}

	// Called whenever the application needs to display the client window. This method writes "Hello, World"
	//
	// Note that this function will not render anything if the window is occluded (e.g. when the screen is locked). Also, this function will
	// automatically discard device-specific resources if the Direct3D device disappears during function invocation, and will recreate the
	// resources the next time it's invoked.
	private void OnRender()
	{
		CreateDeviceResources();

		if (m_pRenderTarget is not null && !m_pRenderTarget!.CheckWindowState().IsFlagSet(D2D1_WINDOW_STATE.D2D1_WINDOW_STATE_OCCLUDED))
		{
			const string sc_helloWorld = "Hello, World!";

			try
			{
				// Retrieve the size of the render target.
				m_pRenderTarget.GetSize(out var renderTargetSize);

				m_pRenderTarget.BeginDraw();

				m_pRenderTarget.SetTransform(D2D_MATRIX_3X2_F.Identity());

				m_pRenderTarget.Clear((D3DCOLORVALUE)Color.White);

				m_pRenderTarget.DrawText(sc_helloWorld, (uint)sc_helloWorld.Length - 1, m_pTextFormat, RectF(0, 0, renderTargetSize.width, renderTargetSize.height), m_pBlackBrush!);

				m_pRenderTarget.EndDraw(out _, out _);
			}
			catch (Exception ex)
			{
				if (ex.HResult == unchecked((int)0x8899000C) /*D2DERR_RECREATE_TARGET*/)
					DiscardDeviceResources();
				else
					throw;
			}
		}
	}

	// If the application receives a WM_SIZE message, this method resizes the render target appropriately.
	private void OnResize(uint width, uint height)
	{
		if (m_pRenderTarget is not null)
		{
			D2D_SIZE_U size;
			size.width = width;
			size.height = height;

			// Note: This method can fail, but it's okay to ignore the error here -- it will be repeated on the next call to EndDraw.
			m_pRenderTarget.Resize(size);
		}
	}
}