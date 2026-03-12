using System;
using System.Diagnostics;
using System.Drawing;
using Vanara.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.D2d1;
using static Vanara.PInvoke.Dwrite;
using static Vanara.PInvoke.DXGI;
using static Vanara.PInvoke.Gdi32;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.User32;
using static Vanara.PInvoke.WindowsCodecs;

internal class DemoApp : VisibleWindow
{
	const string msc_fontName = "Verdana";
	const float msc_fontSize = 50f;

	ID2D1Factory m_pD2DFactory;
	IWICImagingFactory m_pWICFactory;
	IDWriteFactory m_pDWriteFactory;
	IDWriteTextFormat m_pTextFormat;

	ID2D1HwndRenderTarget? m_pRenderTarget;
	ID2D1PathGeometry? m_pPathGeometry;
	ID2D1LinearGradientBrush? m_pLinearGradientBrush;
	ID2D1SolidColorBrush? m_pBlackBrush;
	ID2D1BitmapBrush? m_pGridPatternBitmapBrush;
	ID2D1Bitmap? m_pBitmap;
	ID2D1Bitmap? m_pAnotherBitmap;

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
		//create D2D factory
		m_pD2DFactory = D2D1CreateFactory<ID2D1Factory>();

		// Create a WIC factory.
		m_pWICFactory = new();

		// Create a DirectWrite factory.
		m_pDWriteFactory = DWriteCreateFactory<IDWriteFactory>();

		// Create a DirectWrite text format object.
		m_pTextFormat = m_pDWriteFactory.CreateTextFormat(msc_fontName, default, DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_NORMAL,
			DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_STRETCH.DWRITE_FONT_STRETCH_NORMAL, msc_fontSize, "");

		// Center the text horizontally and vertically.
		m_pTextFormat.SetTextAlignment(DWRITE_TEXT_ALIGNMENT.DWRITE_TEXT_ALIGNMENT_CENTER);

		m_pTextFormat.SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT.DWRITE_PARAGRAPH_ALIGNMENT_CENTER);

		// Create a path geometry.
		m_pPathGeometry = m_pD2DFactory.CreatePathGeometry();

		// Use the geometry sink to write to the path geometry.
		var pSink = m_pPathGeometry.Open();

		pSink.SetFillMode(D2D1_FILL_MODE.D2D1_FILL_MODE_ALTERNATE);

		pSink.BeginFigure(Point2F(0, 0), D2D1_FIGURE_BEGIN.D2D1_FIGURE_BEGIN_FILLED);

		pSink.AddLine(Point2F(200, 0));

		pSink.AddBezier(BezierSegment(Point2F(150, 50), Point2F(150, 150), Point2F(200, 200)));

		pSink.AddLine(Point2F(0, 200));

		pSink.AddBezier(BezierSegment(Point2F(50, 150), Point2F(50, 50), Point2F(0, 0)));

		pSink.EndFigure(D2D1_FIGURE_END.D2D1_FIGURE_END_CLOSED);

		pSink.Close().ThrowIfFailed();
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
		Debug.WriteLine($"WndProc: {(WindowMessage)message}");
		switch ((WindowMessage)message)
		{
			case WindowMessage.WM_SIZE:
				return HANDLE_WM_SIZE(hwnd, wParam, lParam, OnResize);

			case WindowMessage.WM_PAINT:
			case WindowMessage.WM_DISPLAYCHANGE:
				BeginPaint(hwnd, out var ps);
				OnRender();
				EndPaint(hwnd, ps);
				return FALSE;

			default:
				return base.WndProc(hwnd, message, wParam, lParam);
		}
	}

	//
	// This method creates resources which are bound to a particular
	// Direct3D device. It's all centralized here, in case the resources
	// need to be recreated in case of Direct3D device loss (eg. display
	// change, remoting, removal of video card, etc).
	//
	void CreateDeviceResources()
	{
		if (m_pRenderTarget is null)
		{
			GetClientRect(Handle, out var rc);

			D2D_SIZE_U size = (D2D_SIZE_U)rc.Size;

			// Create a Direct2D render target.
			m_pRenderTarget = m_pD2DFactory.CreateHwndRenderTarget(RenderTargetProperties(), HwndRenderTargetProperties(Handle, size));

			// Create a black brush.
			m_pBlackBrush = m_pRenderTarget.CreateSolidColorBrush((D3DCOLORVALUE)Color.Black);

			// Create a linear gradient.
			D2D1_GRADIENT_STOP[] stops = [
				new(0.0f, new(0.0f, 1.0f, 1.0f, 0.25f )),
				new(1.0f, new(0.0f, 0.0f, 1.0f, 1.0f )),
			];

			var pGradientStops = m_pRenderTarget.CreateGradientStopCollection(stops, (uint)stops.Length);

			m_pLinearGradientBrush = m_pRenderTarget.CreateLinearGradientBrush(LinearGradientBrushProperties(Point2F(100, 0),
				Point2F(100, 200)), BrushProperties(), pGradientStops);
		}

		// Create a bitmap from an application resource.
		m_pBitmap = LoadResourceBitmap(m_pRenderTarget,
			m_pWICFactory,
			"SimpleDirect2dApplication.sampleImage.jpg",
			100,
			0);

		// Create a bitmap by loading it from a file.
		m_pAnotherBitmap = LoadBitmapFromFile(m_pRenderTarget,
			m_pWICFactory,
			".\\sampleImage.jpg",
			100,
			0);

		m_pGridPatternBitmapBrush = CreateGridPatternBrush(m_pRenderTarget);
	}

	//
	// Creates a pattern brush.
	//
	ID2D1BitmapBrush CreateGridPatternBrush(ID2D1RenderTarget pRenderTarget)
	{
		// Create a compatible render target.
		var pCompatibleRenderTarget = pRenderTarget.CreateCompatibleRenderTarget(SizeF(10.0f, 10.0f), null, null);

		// Draw a pattern.
		ID2D1SolidColorBrush pGridBrush = pCompatibleRenderTarget.CreateSolidColorBrush(new(0.93f, 0.94f, 0.96f, 1.0f));

		pCompatibleRenderTarget.BeginDraw();
		pCompatibleRenderTarget.FillRectangle(RectF(0.0f, 0.0f, 10.0f, 1.0f), pGridBrush);
		pCompatibleRenderTarget.FillRectangle(RectF(0.0f, 0.1f, 1.0f, 10.0f), pGridBrush);
		pCompatibleRenderTarget.EndDraw();

		// Retrieve the bitmap from the render target.
		ID2D1Bitmap pGridBitmap = pCompatibleRenderTarget.GetBitmap();

		// Choose the tiling mode for the bitmap brush.
		D2D1_BITMAP_BRUSH_PROPERTIES brushProperties = BitmapBrushProperties(D2D1_EXTEND_MODE.D2D1_EXTEND_MODE_WRAP, D2D1_EXTEND_MODE.D2D1_EXTEND_MODE_WRAP);

		// Create the bitmap brush.
		return m_pRenderTarget!.CreateBitmapBrush(pGridBitmap, brushProperties, null);
	}

	//
	// Discard device-specific resources which need to be recreated
	// when a Direct3D device is lost
	//
	void DiscardDeviceResources()
	{
		m_pRenderTarget = null;
		m_pBitmap = null;
		m_pBlackBrush = null;
		m_pLinearGradientBrush = null;
		m_pAnotherBitmap = null;
		m_pGridPatternBitmapBrush = null;
	}

	//
	// Called whenever the application needs to display the client
	// window. This method draws a bitmap a couple times, draws some
	// geometries, and writes "Hello, World"
	//
	// Note that this function will not render anything if the window
	// is occluded (e.g. when the screen is locked).
	// Also, this function will automatically discard device-specific
	// resources if the Direct3D device disappears during function
	// invocation, and will recreate the resources the next time it's
	// invoked.
	//
	void OnRender()
	{
		NoteMethod();
		CreateDeviceResources();

		if ((m_pRenderTarget!.CheckWindowState() & D2D1_WINDOW_STATE.D2D1_WINDOW_STATE_OCCLUDED) == 0)
		{
			const string sc_helloWorld = "Hello, World!";

			// Retrieve the size of the render target.
			D2D_SIZE_F renderTargetSize = m_pRenderTarget.GetSize();

			m_pRenderTarget.BeginDraw();

			m_pRenderTarget.SetTransform(D2D_MATRIX_3X2_F.Identity());

			m_pRenderTarget.Clear(Color.White);

			// Paint a grid background.
			m_pRenderTarget.FillRectangle(RectF(0.0f, 0.0f, renderTargetSize.width, renderTargetSize.height), m_pGridPatternBitmapBrush!);

			D2D_SIZE_F size = m_pBitmap!.GetSize();

			// Draw a bitmap in the upper-left corner of the window.
			m_pRenderTarget.DrawBitmap(m_pBitmap!, RectF(0.0f, 0.0f, size.width, size.height));

			// Draw a bitmap at the lower-right corner of the window.
			size = m_pAnotherBitmap!.GetSize();
			m_pRenderTarget.DrawBitmap(m_pAnotherBitmap!, RectF(renderTargetSize.width - size.width, renderTargetSize.height - size.height,
				renderTargetSize.width, renderTargetSize.height));

			// Set the world transform to a 45 degree rotation at the center of the render target
			// and write "Hello, World".
			m_pRenderTarget.SetTransform(D2D_MATRIX_3X2_F.Rotation(45, Point2F(renderTargetSize.width / 2, renderTargetSize.height / 2)));

			m_pRenderTarget.DrawText(sc_helloWorld, (uint)sc_helloWorld.Length - 1, m_pTextFormat, RectF(0, 0, renderTargetSize.width, renderTargetSize.height), m_pBlackBrush!);

			//
			// Reset back to the identity transform
			//
			m_pRenderTarget.SetTransform(D2D_MATRIX_3X2_F.Translation(0, renderTargetSize.height - 200));

			// Fill the hour glass geometry with a gradient.
			m_pRenderTarget.FillGeometry(m_pPathGeometry!, m_pLinearGradientBrush!);

			m_pRenderTarget.SetTransform(D2D_MATRIX_3X2_F.Translation(renderTargetSize.width - 200, 0));

			// Fill the hour glass geometry with a gradient.
			m_pRenderTarget.FillGeometry(m_pPathGeometry!, m_pLinearGradientBrush!);

			var hr = m_pRenderTarget.EndDraw();
			if (hr == HRESULT.D2DERR_RECREATE_TARGET)
				DiscardDeviceResources();
			else
				hr.ThrowIfFailed(); // Throw if any other error occurred.
		}
	}

	//
	// If the application receives a WM_SIZE message, this method
	// resize the render target appropriately.
	//
	private void OnResize(HWND hWND, WM_SIZE_WPARAM wParam, SIZES sz)
	{
		if (m_pRenderTarget is not null)
		{
			D2D_SIZE_U size = new(sz.Width, sz.Height);

			// Note: This method can fail, but it's okay to ignore the
			// error here -- it will be repeated on the next call to
			// EndDraw.
			m_pRenderTarget.Resize(size);
		}
	}

	//
	// Creates a Direct2D bitmap from a resource in the
	// application resource file.
	//
	public static ID2D1Bitmap LoadResourceBitmap(ID2D1RenderTarget pRenderTarget, IWICImagingFactory pIWICFactory, string resourceName,
		uint destinationWidth, uint destinationHeight)
	{
		NoteMethod();
		// Load the resource.
		using ComStream imgStream = new(System.Reflection.Assembly.GetEntryAssembly()?.
			GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException($"Resource '{resourceName}' not found in assembly resources."));

		// Create a pDecoder for the stream.
		var pDecoder = pIWICFactory.CreateDecoderFromStream(imgStream, SafeGuidPtr.Null, WICDecodeOptions.WICDecodeMetadataCacheOnLoad);

		return LoadBitmapFromDecoder(pRenderTarget, pIWICFactory, destinationWidth, destinationHeight, pDecoder);
	}

	//
	// Creates a Direct2D bitmap from the specified
	// file name.
	//
	public static ID2D1Bitmap LoadBitmapFromFile(ID2D1RenderTarget pRenderTarget, IWICImagingFactory pIWICFactory, string uri, uint destinationWidth, uint destinationHeight)
	{
		NoteMethod();
		var pDecoder = pIWICFactory.CreateDecoderFromFilename(uri, SafeGuidPtr.Null, ACCESS_MASK.GENERIC_READ, WICDecodeOptions.WICDecodeMetadataCacheOnLoad);
		return LoadBitmapFromDecoder(pRenderTarget, pIWICFactory, destinationWidth, destinationHeight, pDecoder);
	}

	private static ID2D1Bitmap LoadBitmapFromDecoder(ID2D1RenderTarget pRenderTarget, IWICImagingFactory pIWICFactory, uint destinationWidth, uint destinationHeight, IWICBitmapDecoder pDecoder)
	{
		// Create the initial pSource.
		var pSource = pDecoder.GetFrame(0);

		// Convert the image format to 32bppPBGRA
		// (DXGI_FORMAT_B8G8R8A8_UNORM + D2D1_ALPHA_MODE_PREMULTIPLIED).
		var pConverter = pIWICFactory.CreateFormatConverter();

		// If a new width or height was specified, create an
		// IWICBitmapScaler and use it to resize the image.
		if (destinationWidth != 0 || destinationHeight != 0)
		{
			pSource.GetSize(out var originalWidth, out var originalHeight);

			if (destinationWidth == 0)
			{
				float scalar = (float)(destinationHeight) / (float)(originalHeight);
				destinationWidth = (uint)(scalar * (float)(originalWidth));
			}
			else if (destinationHeight == 0)
			{
				float scalar = (float)(destinationWidth) / (float)(originalWidth);
				destinationHeight = (uint)(scalar * (float)(originalHeight));
			}

			var pScaler = pIWICFactory.CreateBitmapScaler();
			pScaler.Initialize(pSource, destinationWidth, destinationHeight, WICBitmapInterpolationMode.WICBitmapInterpolationModeCubic);

			pConverter.Initialize(pScaler, WICGuids.GUID_WICPixelFormat32bppPBGRA, WICBitmapDitherType.WICBitmapDitherTypeNone,
				null, default, WICBitmapPaletteType.WICBitmapPaletteTypeMedianCut);
		}
		else
		{
			pConverter.Initialize(pSource, WICGuids.GUID_WICPixelFormat32bppPBGRA, WICBitmapDitherType.WICBitmapDitherTypeNone,
				null, default, WICBitmapPaletteType.WICBitmapPaletteTypeMedianCut);
		}

		//create a Direct2D bitmap from the WIC bitmap.
		return pRenderTarget.CreateBitmapFromWicBitmap(pConverter, default);
	}

	private static void NoteMethod([System.Runtime.CompilerServices.CallerMemberName] string methodName = "") => Debug.WriteLine($"{methodName} called.");
}