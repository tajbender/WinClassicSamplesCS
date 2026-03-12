using System.Drawing;
using Vanara.Extensions;
using Vanara.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.D2d1;
using static Vanara.PInvoke.Dwrite;
using static Vanara.PInvoke.DXGI;
using static Vanara.PInvoke.Gdi32;
using static Vanara.PInvoke.User32;
using static Vanara.PInvoke.WindowsCodecs;

SaveToImageFile();

static void SaveToImageFile()
{
	//
	// Create Factories
	//

	IWICImagingFactory pWICFactory = new();

	ID2D1Factory pD2DFactory = D2D1CreateFactory<ID2D1Factory>();

	IDWriteFactory pDWriteFactory = DWriteCreateFactory<IDWriteFactory>();

	//
	// Create IWICBitmap and RT
	//

	const uint sc_bitmapWidth = 640;
	const uint sc_bitmapHeight = 480;

	IWICBitmap pWICBitmap = pWICFactory.CreateBitmap(sc_bitmapWidth,
		sc_bitmapHeight, WICGuids.GUID_WICPixelFormat32bppBGR,
		WICBitmapCreateCacheOption.WICBitmapCacheOnLoad);

	ID2D1RenderTarget pRT = pD2DFactory.CreateWicBitmapRenderTarget(pWICBitmap,
		RenderTargetProperties());

	//
	// Create text format
	//

	const string sc_fontName = "Calibri";
	const float sc_fontSize = 50;

	IDWriteTextFormat pTextFormat = pDWriteFactory.CreateTextFormat(sc_fontName,
		default,
		DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_NORMAL,
		DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_NORMAL,
		DWRITE_FONT_STRETCH.DWRITE_FONT_STRETCH_NORMAL,
		sc_fontSize,
		""); //locale

	pTextFormat.SetTextAlignment(DWRITE_TEXT_ALIGNMENT.DWRITE_TEXT_ALIGNMENT_CENTER);

	pTextFormat.SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT.DWRITE_PARAGRAPH_ALIGNMENT_CENTER);

	//
	// Create a path geometry representing an hour glass
	//

	ID2D1PathGeometry pPathGeometry = pD2DFactory.CreatePathGeometry();

	ID2D1GeometrySink pSink = pPathGeometry.Open();

	pSink.SetFillMode(D2D1_FILL_MODE.D2D1_FILL_MODE_ALTERNATE);

	pSink.BeginFigure(Point2F(0, 0), D2D1_FIGURE_BEGIN.D2D1_FIGURE_BEGIN_FILLED);

	pSink.AddLine(Point2F(200, 0));

	pSink.AddBezier(BezierSegment(Point2F(150, 50),
		Point2F(150, 150),
		Point2F(200, 200)));

	pSink.AddLine(Point2F(0, 200));

	pSink.AddBezier(BezierSegment(Point2F(50, 150),
		Point2F(50, 50),
		Point2F(0, 0)));

	pSink.EndFigure(D2D1_FIGURE_END.D2D1_FIGURE_END_CLOSED);

	pSink.Close().ThrowIfFailed();

	//
	// Create a linear-gradient brush
	//

	D2D1_GRADIENT_STOP[] stops = [
		new(0f, new(0.0f, 1.0f, 1.0f, 1.0f)),
		new(1f, new(0.0f, 0.0f, 1.0f, 1.0f)),
	];

	ID2D1GradientStopCollection pGradientStops = pRT.CreateGradientStopCollection(stops, (uint)stops.Length);

	ID2D1LinearGradientBrush pLGBrush = pRT.CreateLinearGradientBrush(
		LinearGradientBrushProperties(Point2F(100, 0),
		Point2F(100, 200)),
		BrushProperties(),
		pGradientStops);

	ID2D1SolidColorBrush pBlackBrush = pRT.CreateSolidColorBrush((D3DCOLORVALUE)Color.Black);

	//
	// Render into the bitmap
	//

	pRT.BeginDraw();

	pRT.Clear(Color.White);

	D2D_SIZE_F rtSize = pRT.GetSize();

	// Set the world transform to a 45 degree rotation at the center of the render target
	// and write "Hello, World".
	pRT.SetTransform(D2D_MATRIX_3X2_F.Rotation(45,
		Point2F(rtSize.width / 2,
		rtSize.height / 2)));

	const string sc_helloWorld = "Hello, World!";
	pRT.DrawText(sc_helloWorld, (uint)sc_helloWorld.Length - 1, pTextFormat,
		RectF(0, 0, rtSize.width, rtSize.height), pBlackBrush);

	//
	// Reset back to the identity transform
	//
	pRT.SetTransform(D2D_MATRIX_3X2_F.Translation(0, rtSize.height - 200));

	pRT.FillGeometry(pPathGeometry, pLGBrush);

	pRT.SetTransform(D2D_MATRIX_3X2_F.Translation(rtSize.width - 200, 0));

	pRT.FillGeometry(pPathGeometry, pLGBrush);

	pRT.EndDraw().ThrowIfFailed();

	//
	// Save image to file
	//

	IWICStream pStream = pWICFactory.CreateStream();

	Guid format = WICGuids.GUID_WICPixelFormatDontCare;

	const string filename = "output.png";
	pStream.InitializeFromFilename(filename, ACCESS_MASK.GENERIC_WRITE);

	IWICBitmapEncoder pEncoder = pWICFactory.CreateEncoder(WICGuids.GUID_ContainerFormatPng, SafeGuidPtr.Null);

	pEncoder.Initialize(pStream, WICBitmapEncoderCacheOption.WICBitmapEncoderNoCache);

	pEncoder.CreateNewFrame(out var pFrameEncode, out _);

	pFrameEncode.Initialize(null);

	pFrameEncode.SetSize(sc_bitmapWidth, sc_bitmapHeight);

	pFrameEncode.SetPixelFormat(ref format);

	pFrameEncode.WriteSource(pWICBitmap, default);

	pFrameEncode.Commit();

	pEncoder.Commit();
}