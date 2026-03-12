using System.Drawing;
using Vanara.Extensions;
using Vanara.PInvoke;
using static Vanara.PInvoke.D2d1;
using static Vanara.PInvoke.Dwrite;
using static Vanara.PInvoke.DXGI;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.User32;

[Flags]
enum AnimationStyle
{
	None = 0,
	Translation = 1,
	Rotation = 2,
	Scaling = 4
}

enum TextRenderingMethod
{
	Default,
	Outline,
	UseA8Target,
	NumValues
}

internal class DemoApp : VisibleWindow
{
	uint m_startTime = 0;
	AnimationStyle m_animationStyle = AnimationStyle.Translation;
	TextRenderingMethod m_renderingMethod = TextRenderingMethod.Default;
	D2D_POINT_2F m_overhangOffset;

	ID2D1Factory m_pD2DFactory;
	IDWriteFactory m_pDWriteFactory;
	IDWriteTextFormat m_pTextFormat;
	IDWriteTextLayout m_pTextLayout;
	ID2D1HwndRenderTarget? m_pRT;
	ID2D1SolidColorBrush? m_pBlackBrush;
	ID2D1BitmapRenderTarget? m_pOpacityRT;

	readonly RingBuffer<long> m_times = new(10);

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
	*  DemoApp constructor                                            *
	*                                                                 *
	*  This method is used to create resources which are not bound    *
	*  to any device. Their lifetime effectively extends for the      *
	*  duration of the app. These resources include the D2D,          *
	*  DWrite factories; and a DWrite Text Format object              *
	*  (used for identifying particular font characteristics) and     *
	*  a D2D geometry.                                                *
	*                                                                 *
	******************************************************************/
	public DemoApp()
	{
		const string msc_fontName = "Gabriola";
		const float msc_fontSize = 50;
		const string sc_helloWorld = "The quick brown fox jumped over the lazy dog!";
		uint stringLength = (uint)sc_helloWorld.Length - 1;

		//create D2D factory
		m_pD2DFactory = D2D1CreateFactory<ID2D1Factory>();

		// Create a DirectWrite factory.
		m_pDWriteFactory = DWriteCreateFactory<IDWriteFactory>();

		// Create a DirectWrite text format object.
		m_pTextFormat = m_pDWriteFactory.CreateTextFormat(msc_fontName, default, DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_NORMAL,
			DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_STRETCH.DWRITE_FONT_STRETCH_NORMAL, msc_fontSize, "");

		//center the text horizontally
		m_pTextFormat.SetTextAlignment(DWRITE_TEXT_ALIGNMENT.DWRITE_TEXT_ALIGNMENT_CENTER);

		m_pTextLayout = m_pDWriteFactory.CreateTextLayout(sc_helloWorld,
			stringLength,
			m_pTextFormat,
			300, // maxWidth
			1000); // maxHeight

		//
		// We use typographic features here to show how to account for the
		// overhangs that these features will produce. See the code in
		// ResetAnimation that calls GetOverhangMetrics(). Note that there are
		// fonts that can produce overhangs even without the use of typographic
		// features- this is just one example.
		//
		IDWriteTypography pTypography = m_pDWriteFactory.CreateTypography();
		DWRITE_FONT_FEATURE fontFeature = new() { nameTag = DWRITE_FONT_FEATURE_TAG.DWRITE_FONT_FEATURE_TAG_STYLISTIC_SET_7, parameter = 1 };
		pTypography.AddFontFeature(fontFeature);
		DWRITE_TEXT_RANGE textRange = new() { length = stringLength };
		m_pTextLayout.SetTypography(pTypography, textRange);
	}

	/******************************************************************
	*                                                                 *
	*  CreateDeviceResources                                          *
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

			//
			// Create a D2D render target
			//
			// Note: we only use D2D1_PRESENT_OPTIONS_IMMEDIATELY so that we can
			// easily measure the framerate. Most apps should not use this
			// flag.
			//
			m_pRT = m_pD2DFactory.CreateHwndRenderTarget(RenderTargetProperties(),
				HwndRenderTargetProperties(Handle, size, D2D1_PRESENT_OPTIONS.D2D1_PRESENT_OPTIONS_IMMEDIATELY));

			//
			// Nothing in this sample requires antialiasing so we set the antialias
			// mode to aliased up front.
			//
			m_pRT.SetAntialiasMode(D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);

			//create a black brush
			m_pBlackBrush = m_pRT.CreateSolidColorBrush((D3DCOLORVALUE)Color.Black);

			ResetAnimation(true); // resetClock;
		}
	}

	/******************************************************************
	*                                                                 *
	*  DiscardDeviceResources                                         *
	*                                                                 *
	*  Discard device-specific resources which need to be recreated   *
	*  when a D3D device is lost                                      *
	*                                                                 *
	******************************************************************/
	void DiscardDeviceResources()
	{
		m_pRT = null;
		m_pBlackBrush = null;
		m_pOpacityRT = null;
	}

	/******************************************************************
	*                                                                 *
	*  OnChar                                                         *
	*                                                                 *
	*  Responds to input from the user.                               *
	*                                                                 *
	******************************************************************/
	void OnChar(char key)
	{
		bool resetAnimation = true;
		bool resetClock = true;

		switch (key)
		{
			case 't':
				if (m_animationStyle.IsFlagSet(AnimationStyle.Translation))
					m_animationStyle &= ~AnimationStyle.Translation;
				else
					m_animationStyle |= AnimationStyle.Translation;
				break;

			case 'r':
				if (m_animationStyle.IsFlagSet(AnimationStyle.Rotation))
					m_animationStyle &= ~AnimationStyle.Rotation;
				else
					m_animationStyle |= AnimationStyle.Rotation;
				break;

			case 's':
				if (m_animationStyle.IsFlagSet(AnimationStyle.Scaling))
					m_animationStyle &= ~AnimationStyle.Scaling;
				else
					m_animationStyle |= AnimationStyle.Scaling;
				break;

			case '1':
			case '2':
			case '3':
				m_renderingMethod = (TextRenderingMethod)(key - '1');
				resetClock = false;
				break;

			default:
				resetAnimation = false;
				resetClock = false;
				break;
		}

		if (resetAnimation)
		{
			ResetAnimation(resetClock);
		}
	}

	/******************************************************************
	*                                                                 *
	*  DemoApp::UpdateWindowText                                      *
	*                                                                 *
	*  This method updates the window title bar with info about the   *
	*  current animation style and rendering method. It also outputs  *
	*  the framerate.                                                 *
	*                                                                 *
	******************************************************************/
	void UpdateWindowText()
	{
		long sc_lastTimeStatusShown = 0;

		//
		// Update the window status no more than 10 times a second. Without this
		// check, the performance bottleneck could potentially be the time it takes
		// for Windows to update the title.
		//
		if (m_times.Count > 0 && m_times.Last > sc_lastTimeStatusShown + 1000000)
		{
			//
			// Determine the frame rate by computing the difference in clock time
			// between this frame and the frame we rendered 10 frames ago.
			//
			sc_lastTimeStatusShown = m_times.Last;

			QueryPerformanceFrequency(out var frequency);

			float fps = 0.0f;
			if (m_times.Count > 0)
			{
				fps = (m_times.Count - 1) * frequency / (float)((m_times.Last - m_times.First));
			}

			//
			// Add other useful information to the window title.
			//

			string style = m_animationStyle switch
			{
				AnimationStyle.None => "None",
				AnimationStyle.Translation => "Translation",
				AnimationStyle.Rotation => "Rotation",
				AnimationStyle.Scaling => "Scale",
				_ => "",
			};

			string method = m_renderingMethod switch
			{
				TextRenderingMethod.Default => "Default",
				TextRenderingMethod.Outline => "Outline",
				TextRenderingMethod.UseA8Target => "UseA8Target",
				_ => "",
			};

			string title = string.Format("AnimationStyle: {0}{1}{2}, Method: {3}, {4:F1} fps",
				m_animationStyle.IsFlagSet(AnimationStyle.Translation) ? "+t" : "-t",
				m_animationStyle.IsFlagSet(AnimationStyle.Rotation) ? "+r" : "-r",
				m_animationStyle.IsFlagSet(AnimationStyle.Scaling) ? "+s" : "-s",
				method,
				fps);

			if (!Handle.IsInvalid)
				SetWindowText(Handle, title);
		}
	}

	/******************************************************************
	*                                                                 *
	*  DemoApp::ResetAnimation                                        *
	*                                                                 *
	*  This method does the necessary work to change the current      *
	*  animation style.                                               *
	*                                                                 *
	******************************************************************/
	void ResetAnimation(bool resetClock)
	{
		if (resetClock)
		{
			m_startTime = GetTickCount();
		}

		//
		// Release the opacity mask. We will regenerate it if the current animation
		// style demands it.
		//
		m_pOpacityRT = null;

		if (m_renderingMethod == TextRenderingMethod.Outline)
		{
			//
			// Set the rendering mode to OUTLINE mode. To do this we first create
			// a default params object and then make a copy with the given modification.
			//
			IDWriteRenderingParams pDefaultParams = m_pDWriteFactory.CreateRenderingParams();

			IDWriteRenderingParams pRenderingParams = m_pDWriteFactory.CreateCustomRenderingParams(pDefaultParams.GetGamma(),
				pDefaultParams.GetEnhancedContrast(),
				pDefaultParams.GetClearTypeLevel(),
				pDefaultParams.GetPixelGeometry(),
				DWRITE_RENDERING_MODE.DWRITE_RENDERING_MODE_OUTLINE);

			m_pRT!.SetTextRenderingParams(pRenderingParams);
		}
		else
		{
			// Reset the rendering mode to default.
			m_pRT!.SetTextRenderingParams(default);
		}

		if (m_renderingMethod == TextRenderingMethod.UseA8Target)
		{
			//
			// Create a compatible A8 Target to store the text as an opacity mask.
			//
			// Note: To reduce sampling error in the scale animation, it might be
			// preferable to create multiple masks for the text at different
			// resolutions.
			//
			m_pRT.GetDpi(out var dpiX, out var dpiY);

			//
			// It is important to obtain the overhang metrics here in case the text
			// extends beyond the layout max-width and max-height.
			//
			var overhangMetrics = m_pTextLayout.GetOverhangMetrics();

			//
			// Because the overhang metrics can be off slightly given that these
			// metrics do not account for antialiasing, we add an extra pixel for
			// padding.
			//
			D2D_SIZE_F padding = SizeF(96.0f / dpiX, 96.0f / dpiY);
			m_overhangOffset = Point2F((float)Math.Ceiling(overhangMetrics.left + padding.width), (float)Math.Ceiling(overhangMetrics.top + padding.height));

			//
			// The true width of the text is the max width + the overhang
			// metrics + padding in each direction.
			//
			D2D_SIZE_F maskSize = SizeF(overhangMetrics.right + padding.width + m_overhangOffset.x + m_pTextLayout.GetMaxWidth(),
			overhangMetrics.bottom + padding.height + m_overhangOffset.y + m_pTextLayout.GetMaxHeight());

			// Round up to the nearest pixel
			D2D_SIZE_U maskPixelSize = SizeU((uint)(Math.Ceiling(maskSize.width * dpiX / 96.0f)),
				(uint)(Math.Ceiling(maskSize.height * dpiY / 96.0f)));


			//
			// Create the compatible render target using desiredPixelSize to avoid
			// blurriness issues caused by a fractional-pixel desiredSize.
			//
			D2D1_PIXEL_FORMAT alphaOnlyFormat = PixelFormat(DXGI_FORMAT.DXGI_FORMAT_A8_UNORM, D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED);
			m_pOpacityRT = m_pRT.CreateCompatibleRenderTarget(default, maskPixelSize, alphaOnlyFormat, D2D1_COMPATIBLE_RENDER_TARGET_OPTIONS.D2D1_COMPATIBLE_RENDER_TARGET_OPTIONS_NONE);

			//
			// Draw the text to the opacity mask. Note that we can use pixel
			// snapping now given that subpixel translation can now happen during
			// the FillOpacityMask method.
			//
			m_pOpacityRT.BeginDraw();
			m_pOpacityRT.Clear(new D3DCOLORVALUE(Color.Black, 0.0f));
			m_pOpacityRT.DrawTextLayout(m_overhangOffset, m_pTextLayout, m_pBlackBrush!, D2D1_DRAW_TEXT_OPTIONS.D2D1_DRAW_TEXT_OPTIONS_NO_SNAP);
			m_pOpacityRT.EndDraw();
		}
	}

	/******************************************************************
	*                                                                 *
	*  DemoApp::CalculateTransform                                    *
	*                                                                 *
	*  Calculates the transform based on the current time             *
	*                                                                 *
	******************************************************************/
	void CalculateTransform(out D2D_MATRIX_3X2_F pTransform)
	{
		// calculate a 't' value that will linearly interpolate from 0 to 1 and back every 20 seconds
		uint currentTime = GetTickCount();
		if (m_startTime == 0)
		{
			m_startTime = currentTime;
		}
		float t = 2 * ((currentTime - m_startTime) % 20000) / 20000.0f;
		if (t > 1.0f)
		{
			t = 2 - t;
		}

		// range from -100 to 100
		float translationOffset = m_animationStyle.IsFlagSet(AnimationStyle.Translation) ? (t - 0.5f) * 200 : 0f;

		// range from 0 to 360
		float rotation = m_animationStyle.IsFlagSet(AnimationStyle.Rotation) ? t * 360.0f : 0f;

		// range from 1/4 to 2x the normal size
		float scaleMultiplier = m_animationStyle.IsFlagSet(AnimationStyle.Scaling) ? t * 1.75f + 0.25f : 1.0f;

		m_pRT!.GetSize(out var size);

		pTransform = D2D_MATRIX_3X2_F.Rotation(rotation) *
			D2D_MATRIX_3X2_F.Scale(scaleMultiplier, scaleMultiplier) *
			D2D_MATRIX_3X2_F.Translation(translationOffset + size.width / 2.0f, translationOffset + size.height / 2.0f);
	}

	/******************************************************************
	*                                                                 *
	*  DemoApp::OnRender                                              *
	*                                                                 *
	*  Called whenever the application needs to display the client    *
	*  window.                                                        *
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
		//
		// We use a ring buffer to store the clock time for the last 10 frames.
		// This lets us eliminate a lot of noise when computing framerate.
		//
		QueryPerformanceCounter(out var time);
		m_times.Add(time);

		CreateDeviceResources();

		if (!m_pRT!.CheckWindowState().IsFlagSet(D2D1_WINDOW_STATE.D2D1_WINDOW_STATE_OCCLUDED))
		{
			CalculateTransform(out var transform);

			m_pRT.BeginDraw();

			m_pRT.Clear(Color.White);

			m_pRT.SetTransform(transform);

			DWRITE_TEXT_METRICS textMetrics = m_pTextLayout.GetMetrics();

			if (m_renderingMethod == TextRenderingMethod.UseA8Target)
			{
				//
				// Offset the destination rect such that the text will be centered
				// on the render target. Given that we have offset the text in the
				// A8 target by the overhang offset, we must factor that into the
				// destination rect now.
				//
				D2D_SIZE_F opacityRTSize = m_pOpacityRT!.GetSize();
				D2D_POINT_2F offset = Point2F(-textMetrics.width / 2.0f - m_overhangOffset.x, -textMetrics.height / 2.0f - m_overhangOffset.y);

				//
				// Round the offset to the nearest pixel. Note that the rounding
				// done here is unecessary, but it causes the text to be less
				// blurry.
				//
				m_pRT.GetDpi(out var dpiX, out var dpiY);
				D2D_POINT_2F roundedOffset = Point2F((float)Math.Floor(offset.x * dpiX / 96.0f + 0.5f) * 96.0f / dpiX,
					(float)Math.Floor(offset.y * dpiY / 96.0f + 0.5f) * 96.0f / dpiY);

				D2D_RECT_F destinationRect = RectF(roundedOffset.x,
					roundedOffset.y,
					roundedOffset.x + opacityRTSize.width,
					roundedOffset.y + opacityRTSize.height);

				ID2D1Bitmap pBitmap = m_pOpacityRT!.GetBitmap();

				pBitmap.GetDpi(out dpiX, out dpiY);

				//
				// The antialias mode must be set to D2D1_ANTIALIAS_MODE_ALIASED
				// for this method to succeed. We've set this mode already though
				// so no need to do it again.
				//
				m_pRT.FillOpacityMask(pBitmap, m_pBlackBrush!, D2D1_OPACITY_MASK_CONTENT.D2D1_OPACITY_MASK_CONTENT_TEXT_NATURAL, destinationRect);
			}
			else
			{
				// Disable pixel snapping to get a smoother animation.
				m_pRT.DrawTextLayout(Point2F(-textMetrics.width / 2.0f, -textMetrics.height / 2.0f),
					m_pTextLayout,
					m_pBlackBrush!,
					D2D1_DRAW_TEXT_OPTIONS.D2D1_DRAW_TEXT_OPTIONS_NO_SNAP);
			}

			var hr = m_pRT.EndDraw();

			if (hr == HRESULT.D2DERR_RECREATE_TARGET)
			{
				DiscardDeviceResources();
			}

			// To animate as quickly as possible, we request another WM_PAINT
			// immediately.
			InvalidateRect(Handle, default, false);
		}

		UpdateWindowText();
	}

	/******************************************************************
	*                                                                 *
	*  DemoApp::OnResize                                              *
	*                                                                 *
	*  If the application receives a WM_SIZE message, this method     *
	*  resizes the render target appropriately.                       *
	*                                                                 *
	******************************************************************/
	void OnResize(uint width, uint height)
	{
		if (m_pRT is not null)
		{
			D2D_SIZE_U size = new() { width = width, height = height };

			// Note: This method can fail, but it's okay to ignore the
			// error here -- it will be repeated on the next call to
			// EndDraw.
			m_pRT.Resize(size);
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
				return FALSE;

			case WindowMessage.WM_CHAR:
				OnChar((char)Macros.LOWORD(wParam));
				return FALSE;

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

	private static class DPIScale
	{
		private static readonly float scaleX, scaleY;

		static DPIScale()
		{
			var dpi = GetDpiForWindow(GetDesktopWindow());
			scaleX = scaleY = dpi / 96.0f;
		}

		public static SIZE Scale(int width, int height) => new((int)Math.Ceiling(width * scaleX), (int)Math.Ceiling(height * scaleY));
	}
}