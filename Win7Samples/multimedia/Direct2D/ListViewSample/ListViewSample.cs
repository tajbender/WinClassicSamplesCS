using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using Vanara.Extensions;
using Vanara.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.D2d1;
using static Vanara.PInvoke.Dwrite;
using static Vanara.PInvoke.DXGI;
using static Vanara.PInvoke.Gdi32;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.Macros;
using static Vanara.PInvoke.Ole32;
using static Vanara.PInvoke.Shell32;
using static Vanara.PInvoke.User32;
using static Vanara.PInvoke.WindowsCodecs;

internal class ListViewApp : VisibleWindow
{
	HWND m_d2dHwnd;

	readonly ID2D1Factory m_pD2DFactory;
	readonly IWICImagingFactory m_pWICFactory;
	readonly IDWriteFactory m_pDWriteFactory;
	readonly IDWriteTextFormat m_pTextFormat;

	ID2D1HwndRenderTarget? m_pRT;
	ID2D1SolidColorBrush? m_pBlackBrush;
	IBindCtx? m_pBindContext;
	ID2D1Bitmap? m_pBitmapAtlas;


	// Size of bitmap atlas (in pixels)
	const uint msc_atlasWidth = 2048;
	const uint msc_atlasHeight = 2048;

	// Width/Height of each icon
	const uint msc_iconSize = 48;

	// Space between each item
	const uint msc_lineSpacing = 10;

	// Number of frames to show while animating item repositioning
	const uint msc_totalAnimatingItemFrames = 60;

	// Number of frames to show while animating scrolls
	const uint msc_totalAnimatingScrollFrames = 10;

	// Static size of item info array
	const uint msc_maxItemInfos = msc_atlasHeight * msc_atlasWidth / (msc_iconSize * msc_iconSize);

	readonly ItemInfo[] m_pFiles = new ItemInfo[msc_maxItemInfos];

	// Number of item infos actually loaded (<= msc_maxItemInfos)
	uint m_numItemInfos;

	// Maximum scroll amount
	uint m_scrollRange;

	// m_currentScrollPos is the current scroll position. We animate to the
	// current scroll position from the previous scroll position,
	// m_previousScrollPos, interpolating between the two based on the factor
	// m_animatingItems / msc_totalAnimatingScrollFrames.
	int m_previousScrollPos;
	int m_currentScrollPos;
	uint m_animatingScroll;

	readonly WindowClass m_childwc;

	// m_animatingItems / msc_totalAnimatingItemFrames is the interpolation
	// factor for animating between the previousPosition and currentPosition of
	// each ItemInfo
	uint m_animatingItems;

	/******************************************************************
	*                                                                 *
	*  ListViewApp::ListViewApp constructor                           *
	*                                                                 *
	*  Initialize member data                                         *
	*  Create application window and device-independent resources.    *
	*                                                                 *
	******************************************************************/
	public ListViewApp()
	{
		const string msc_fontName = "Calibri";
		const float msc_fontSize = 20f;

		// Create a Direct2D factory.
		m_pD2DFactory = D2D1CreateFactory<ID2D1Factory>();

		// Create WIC factory
		m_pWICFactory = new();

		// Create a DirectWrite factory.
		m_pDWriteFactory = DWriteCreateFactory<IDWriteFactory>();

		// Create a DirectWrite text format object.
		m_pTextFormat = m_pDWriteFactory.CreateTextFormat(msc_fontName, default, DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_THIN,
			DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_STRETCH.DWRITE_FONT_STRETCH_NORMAL, msc_fontSize, "");

		// Center the text horizontally and vertically.
		m_pTextFormat.SetTextAlignment(DWRITE_TEXT_ALIGNMENT.DWRITE_TEXT_ALIGNMENT_LEADING);
		m_pTextFormat.SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT.DWRITE_PARAGRAPH_ALIGNMENT_CENTER);

		{
			var pEllipsis = m_pDWriteFactory.CreateEllipsisTrimmingSign(m_pTextFormat);

			DWRITE_TRIMMING sc_trimming = new() { granularity = DWRITE_TRIMMING_GRANULARITY.DWRITE_TRIMMING_GRANULARITY_CHARACTER };

			// Set the trimming back on the trimming format object.
			m_pTextFormat.SetTrimming(sc_trimming, pEllipsis);
		}

		// Set the text format not to allow word wrapping.
		m_pTextFormat.SetWordWrapping(DWRITE_WORD_WRAPPING.DWRITE_WORD_WRAPPING_NO_WRAP);

		m_childwc = new("D2DListViewApp", wndProc: ChildWndProc);

		Created += () =>
		{
			D2D_SIZE_U d2dWindowSize = CalculateD2DWindowSize();
			m_d2dHwnd = AddChild<int>("D2DListViewApp", 0, 0, (int)d2dWindowSize.width, (int)d2dWindowSize.height, style: WindowStyles.WS_CHILDWINDOW | WindowStyles.WS_VISIBLE);
		};
	}

	/******************************************************************
	*                                                                 *
	*  WinMain                                                        *
	*                                                                 *
	*  Application entrypoint                                         *
	*                                                                 *
	******************************************************************/
	public static void Main() => Run<ListViewApp>("D2D ListView", DPIScale.Scale(640, 480));

	/******************************************************************
	*                                                                 *
	*  ListViewApp::CalculateD2DWindowSize                            *
	*                                                                 *
	*  Determine the size of the D2D child window.                    *
	*                                                                 *
	******************************************************************/
	D2D_SIZE_U CalculateD2DWindowSize()
	{
		_ = GetClientRect(Handle, out var rc);
		return SizeU((uint)rc.right, (uint)rc.bottom);
	}

	/******************************************************************
	*                                                                 *
	* ListViewApp::CreateDeviceResources							  *
	* This method creates resources which are bound to a particular   *
	* D3D device. It's all centralized here, in case the 			  *
	* resources need to be recreated in case of D3D device loss (eg.  *
	* display change, remoting, removal of video card, etc).		  *
	*                                                                 *
	******************************************************************/
	void CreateDeviceResources()
	{
		if (m_pRT is null)
		{
			_ = GetClientRect(m_d2dHwnd, out var rc);

			D2D_SIZE_U size = SizeU((uint)rc.right - (uint)rc.left, (uint)rc.bottom - (uint)rc.top);

			//create a D2D render target
			m_pRT = m_pD2DFactory.CreateHwndRenderTarget(RenderTargetProperties(), HwndRenderTargetProperties(m_d2dHwnd, size));

			//create a black brush
			m_pBlackBrush = m_pRT.CreateSolidColorBrush(new(Color.Black));

			m_pBitmapAtlas = m_pRT.CreateBitmap(SizeU(msc_atlasWidth, msc_atlasHeight), default, default, BitmapProperties(PixelFormat(DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED)));

			CreateBindCtx(0, out m_pBindContext).ThrowIfFailed();
			LoadDirectory();
		}
	}

	/******************************************************************
	*                                                                 *
	*  ListViewApp::DiscardDeviceResources                            *
	*                                                                 *
	*  Discard device-specific resources which need to be recreated   *
	*  when a D3D device is lost                                      *
	*                                                                 *
	******************************************************************/
	void DiscardDeviceResources()
	{
		m_pRT = null;
		m_pBitmapAtlas = null;
		m_pBlackBrush = null;
	}

	/******************************************************************
	*                                                                 *
	*  ListViewApp::LoadDirectory                                     *
	*                                                                 *
	*  Load item info from files in the current directory.            *
	*  Thumbnails/Icons are also loaded into the atlas and their      *
	*  location is stored within each ItemInfo object.                *
	*                                                                 *
	******************************************************************/
	void LoadDirectory()
	{
		HRESULT hr = HRESULT.S_OK;

		// Locals that need to be cleaned up before we exit
		using var memoryDC = CreateCompatibleDC(default);
		//ref byte pBits = default;
		//IShellItemImageFactory pShellItemImageFactory = default;

		// Other locals
		const uint sc_bitsArraySize = msc_iconSize * msc_iconSize * 4; // 4 bytes per pixel in BGRA format.
		//uint absolutePathArraySize = 0;

		using SafeHGlobalHandle pBits = new(sc_bitsArraySize);

		// We have a static array of ItemInfo objects, so we can only load
		// msc_maxItemInfos ItemInfos.
		uint currentX = 0;
		uint currentY = 0;
		uint i = 0;
		SafeSearchHandle directoryTraversalHandle = SafeSearchHandle.Null;
		SafeHBITMAP iconImage = SafeHBITMAP.Null;
		while (i < msc_maxItemInfos)
		{
			// We always load files from the current directory. We do navigation
			// by changing the current directory. The first time through the
			// while loop directoryTraversalHandle will be equal to
			// INVALID_HANDLE_VALUE and so we'll call FindFirstFile. On
			// subsequent interations we'll call FindNextFile to find other
			// items in the current directory.
			WIN32_FIND_DATA findFileData = default;
			if (directoryTraversalHandle.IsInvalid)
			{
				_ = Win32Error.ThrowLastErrorIfInvalid(directoryTraversalHandle = FindFirstFile(".\\*", out findFileData));
			}
			else
			{
				if (!FindNextFile(directoryTraversalHandle, out findFileData))
				{
					var err = Win32Error.GetLastError();
					err.ThrowUnless(Win32Error.ERROR_NO_MORE_FILES, "Failed to find next file in directory.");
					break;
				}
			}

			m_pFiles[i] = new ItemInfo() { placement = new(currentX, currentY, 0, 0) };

			//
			// Increment bitmap atlas position here so that we notice if we
			// don't have enough room for any more icons.
			//
			currentX += msc_iconSize;

			if (currentX + msc_iconSize > msc_atlasWidth)
			{
				currentX = 0;
				currentY += msc_iconSize;
			}

			if (currentY + msc_iconSize > msc_atlasHeight)
			{
				// Exceeded atlas size
				// We break without any error so that the contents up until this
				// point will be shown.
				break;
			}

			//
			// Determine the size of array needed to store the full path name.
			// We need the full path name to call SHCreateItemFromParsingName.
			//
			uint requiredLength = GetFullPathName(findFileData.cFileName, 0, default, out _);
			_ = Win32Error.ThrowLastErrorIf(requiredLength, v => v == 0);
			string wszAbsolutePath = GetFullPathName(findFileData.cFileName, out _)!;

			// Create an IShellItemImageFactory for the current directory item
			// so that we can get a icon/thumbnail for it.
			IShellItemImageFactory pShellItemImageFactory = SHCreateItemFromParsingName<IShellItemImageFactory>(wszAbsolutePath, m_pBindContext)!;

			SIZE iconSize = new((int)msc_iconSize, (int)msc_iconSize);

			// If iconImage isn't default that means we're looping around. We call
			// DeleteObject to avoid leaking the HBITMAP.
			if (!iconImage.IsInvalid)
			{
				_ = DeleteObject(iconImage);
				iconImage.Dispose();
			}

			// Get the icon/thumbnail for the current directory item in HBITMAP
			// form.
			// In the interests of brevity, this sample calls GetImage from the
			// UI thread. However this function can be time consuming, so a real
			// application should call GetImage from a separate thread, showing
			// a placeholder icon until the icon has been loaded.
			hr = pShellItemImageFactory.GetImage(iconSize, 0x0, out iconImage);
			if (hr.Failed)
			{
				break;
			}

			SafeBITMAPINFO bi = new(new BITMAPINFO { bmiHeader = BITMAPINFOHEADER.Default });

			// Get the bitmap info header.
			_ = Win32Error.ThrowLastErrorIf(GetDIBits(memoryDC, // hdc
				iconImage, // hbmp
				0, // uStartScan
				0, // cScanLines
				default, // lpvBits
				bi,
				DIBColorMode.DIB_RGB_COLORS), v => v == 0);

			ref BITMAPINFOHEADER bih = ref bi.DangerousGetHandle().AsRef<BITMAPINFOHEADER>();

			// Positive bitmap info header height means bottom-up bitmaps. We
			// always use top-down bitmaps, so we set the height negative.
			if (bih.biHeight > 0)
			{
				bih.biHeight = -bih.biHeight;
			}

			// If we happen to find an icon that's too big, skip over this item.
			if ((-bih.biHeight > msc_iconSize)
				|| (bih.biWidth > msc_iconSize)
				|| (bih.biSizeImage > sc_bitsArraySize))
			{
				continue;
			}

			m_pFiles[i].isDirectory = findFileData.dwFileAttributes.IsFlagSet(FileAttributes.Directory);

			// Now that we know the size of the icon/thumbnail we can initialize
			// the rest of placement rectangle. We avoid using currentX/currentY
			// since we've already incremented those values in anticipation of
			// the next iteration of the loop.
			m_pFiles[i].placement.right = m_pFiles[i].placement.left + (uint)bih.biWidth;
			m_pFiles[i].placement.bottom = (uint)(m_pFiles[i].placement.top + -bih.biHeight);

			// Now we copy the bitmap bits into a buffer.
			_ = Win32Error.ThrowLastErrorIf(GetDIBits(memoryDC, iconImage, 0, (uint)Math.Abs(bih.biHeight),
				pBits, bi, DIBColorMode.DIB_RGB_COLORS), v => v == 0);

			// Now we copy the buffer into video memory.
			try { m_pBitmapAtlas!.CopyFromMemory(m_pFiles[i].placement, pBits, bih.biSizeImage / (uint)Math.Abs(bih.biHeight)); }
			catch { break; }

			m_pFiles[i].szFilename = findFileData.cFileName;

			// Set the previous position to 0 so that the items animate
			// downwards when they are first shown.
			m_pFiles[i].previousPosition = 0.0f;
			m_pFiles[i].currentPosition = (float)(i * (msc_iconSize + msc_lineSpacing));
			i++;
		}

		m_numItemInfos = i;

		//
		// The total size of our document.
		//
		m_scrollRange = msc_iconSize * m_numItemInfos + msc_lineSpacing * (m_numItemInfos - 1);

		SCROLLINFO si = new(SIF.SIF_DISABLENOSCROLL | SIF.SIF_PAGE | SIF.SIF_POS | SIF.SIF_RANGE)
		{
			nMax = (int)m_scrollRange,
			nPage = (uint)(m_pRT!.GetSize().height),
		};
		_ = SetScrollInfo(Handle, SB.SB_VERT, si, true);

		//
		// Animate the item positions into place.
		//
		m_animatingItems = msc_totalAnimatingItemFrames;

		//
		// Set the scroll to zero, don't animate.
		//
		m_animatingScroll = 0;
		m_currentScrollPos = 0;
		m_previousScrollPos = 0;

		//
		// Clean up locals.
		//
		if (!iconImage.IsInvalid)
		{
			_ = DeleteObject(iconImage);
		}
	}

	/******************************************************************
	*                                                                 *
	*  ListViewApp::OnRender                                          *
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
		CreateDeviceResources();

		if (!m_pRT!.CheckWindowState().IsFlagSet(D2D1_WINDOW_STATE.D2D1_WINDOW_STATE_OCCLUDED))
		{
			// We animate scrolling to achieve a smooth scrolling effect.
			// GetInterpolatedScrollPosition() returns the scroll position
			// for the current frame.
			float interpolatedScroll = GetInterpolatedScrollPosition();

			m_pRT.BeginDraw();

			// Displaying the correctly scrolled view is as simple as setting the
			// transform to translate by the current scroll amount.
			m_pRT.SetTransform(D2D_MATRIX_3X2_F.Translation(0, -interpolatedScroll));

			m_pRT.Clear(Color.White);

			D2D_SIZE_F rtSize = m_pRT.GetSize();

			float interpolationFactor = GetAnimatingItemInterpolationFactor();

			for (uint i = 0; i < m_numItemInfos; i++)
			{
				Debug.Assert(m_pFiles[i].szFilename != "");

				// We animate item position changes. The interpolation factor is the
				// a ratio between 0 and 1 used to interpolate between the previous
				// position and the current position. The position that we draw for
				// this frame is somewhere between the two.
				float interpolatedPosition = GetFancyAccelerationInterpolatedValue(interpolationFactor,
					m_pFiles[i].previousPosition, m_pFiles[i].currentPosition);

				// We do a quick check to see if the items we are drawing will be in
				// the visible region. If they are not, we don't bother issues the
				// draw commands. This is a substantial perf win.
				float topOfIcon = interpolatedPosition;
				float bottomOfIcon = interpolatedPosition + msc_iconSize;

				if (bottomOfIcon < interpolatedScroll || topOfIcon > interpolatedScroll + m_pRT.GetSize().height)
				{
					// Some further items could be in the visible region. Continue
					// the loop so that they will be drawn.
					continue;
				}

				// When the items change position we draw them mostly transparent
				// and then gradually make them more opaque as they get closer to
				// their final positions. This function was chosen after a bit of
				// experimentation and I thought it looked nice.
				float opacity = Math.Max(0.2f, interpolationFactor * interpolationFactor);

				// The icon is stored in the image atlas. We reference it's position
				// in the atlas and it's destination on the screen.
				m_pRT.DrawBitmap(m_pBitmapAtlas!, RectF(0, interpolatedPosition, msc_iconSize, interpolatedPosition + msc_iconSize),
					opacity, D2D1_BITMAP_INTERPOLATION_MODE.D2D1_BITMAP_INTERPOLATION_MODE_LINEAR,
					RectF( m_pFiles[i].placement.left, m_pFiles[i].placement.top, m_pFiles[i].placement.right, m_pFiles[i].placement.bottom));

				// Draw the filename. For brevity we just use DrawText. A real
				// application should consider caching the TextLayout object during
				// animations to reduce CPU cost.
				m_pBlackBrush!.SetOpacity(opacity);
				m_pRT.DrawText(m_pFiles[i].szFilename, (uint)m_pFiles[i].szFilename.Length, m_pTextFormat,
					RectF(msc_iconSize + msc_lineSpacing, interpolatedPosition, rtSize.width, interpolatedPosition + msc_iconSize), m_pBlackBrush);
			}

			if (m_pRT.EndDraw() == HRESULT.D2DERR_RECREATE_TARGET)
			{
				DiscardDeviceResources();
			}
		}

		// Advance the position of the current item animation.
		if (m_animatingItems > 0)
		{
			--m_animatingItems;
			if (m_animatingItems == 0)
			{
				for (uint i = 0; i < m_numItemInfos; i++)
				{
					m_pFiles[i].previousPosition = m_pFiles[i].currentPosition;
				}
			}

			_ = InvalidateRect(m_d2dHwnd, default, false);
		}

		// Advance the position of the current scroll animation
		if (m_animatingScroll > 0)
		{
			--m_animatingScroll;
			if (m_animatingScroll == 0)
			{
				m_previousScrollPos = m_currentScrollPos;
			}

			_ = InvalidateRect(m_d2dHwnd, default, false);
		}
	}

	/******************************************************************
	*                                                                 *
	*  ListViewApp::GetFancyAccelerationInterpolatedValue             *
	*                                                                 *
	*  Do a fancy interpolation between two points.                   *
	*                                                                 *
	******************************************************************/
	static float GetFancyAccelerationInterpolatedValue(float linearFactor, float p1, float p2)
	{
		Debug.Assert(linearFactor is >= 0.0f and <= 1.0f);

		// Don't overshoot by more than the icon size.
		float apex = Math.Abs(p1 - p2) > 0.01f ? Math.Min(0.10f * Math.Abs(p1 - p2), msc_iconSize) / Math.Abs(p1 - p2) : 0.0f;

		// Stretch so that the initial overshoot (occurring 33% of the way along) is
		// 70% of the animation.
		float rearrangedDomain = linearFactor < 0.7f ? (linearFactor / 0.7f) / 3.0f : ((linearFactor - 0.7f) / 0.3f) * (2.0f / 3.0f) + 1.0f / 3.0f;

		//
		// We will use sin to approximate the curve. Since we want to start at a
		// minimum value, we start at -PI/2. Since we want to finish at the second
		// max. We stretch the interval [0..1] to [-PI/2 .. 5PI/2].
		//

		float stretchedDomain = rearrangedDomain * 3.0f * (float)Math.PI;
		float translatedDomain = stretchedDomain - (float)Math.PI;

		float fancyFactor = (float)Math.Sin(translatedDomain) + 1.0f; // Now between 0 and 2

		//
		// Before the first max, we want the bounds to go from 0 to 1+apex
		//
		if (translatedDomain < (float)(Math.PI))
		{
			fancyFactor = fancyFactor * (1.0f + apex) / 2.0f; // Now between 0 and 1+apex
		}
		//
		// After the first max, we want to ease the bounds down so that when
		// translatedDomain is 5PI/2, fancyFactor is 1.0f. We also want the bounce
		// to be small, so we reduce the magnitude of the oscillation.
		//
		else
		{
			//
			// When we want the bounce (the undershoot after reaching the apex), to
			// be reach 1.0f - apex / 2.0f at a minimum.
			//
			float oscillationMin = (1.0f - apex / 2.0f);

			//
			// We want the max to start out at 1.0f + apex (so that we are
			// continuous) and finish at 1.0f (the final position). We square our
			// interpolation factor to stretch the bounce and compress the
			// correction. Since the correction is a smaller distance, this looks
			// better. Another benefit is that it prevents us from overshooting 1.0f
			// during the correction phase.
			//
			float interpolationFactor = (translatedDomain - (float)Math.PI) / (2.0f * (float)Math.PI);
			interpolationFactor *= interpolationFactor;
			float oscillationMax = 1.0f * interpolationFactor + (1.0f + apex) * (1.0f - interpolationFactor);

			Debug.Assert(oscillationMax >= oscillationMin);

			float oscillationMidPoint = (oscillationMin + oscillationMax) / 2.0f;

			float oscillationMagnitude = oscillationMax - oscillationMin;

			// Oscillate around the midpoint
			fancyFactor = (fancyFactor / 2.0f - 0.5f) * oscillationMagnitude + oscillationMidPoint;
		}

		return p2 * fancyFactor + p1 * (1.0f - fancyFactor);
	}

	/******************************************************************
	*                                                                 *
	*  ListViewApp::GetAnimatingItemInterpolationFactor               *
	*                                                                 *
	*  Return the interpolation factor for a linear interpolation     *
	*  for the current item animation for the the current frame       *
	*                                                                 *
	******************************************************************/
	float GetAnimatingItemInterpolationFactor() => (float)(msc_totalAnimatingItemFrames - m_animatingItems) / msc_totalAnimatingItemFrames;

	/******************************************************************
	*                                                                 *
	*  ListViewApp::GetAnimatingScrollInterpolationFactor             *
	*                                                                 *
	*  Return the interpolation factor for a linear interpolation     *
	*  for the current scroll animation for the the current frame     *
	*                                                                 *
	******************************************************************/
	float GetAnimatingScrollInterpolationFactor() => (float)(msc_totalAnimatingScrollFrames - m_animatingScroll) / msc_totalAnimatingScrollFrames;

	/******************************************************************
	*                                                                 *
	*  ListViewApp::GetInterpolatedScrollPosition                     *
	*                                                                 *
	*  Return the scroll position for the current frame               *
	*                                                                 *
	******************************************************************/
	float GetInterpolatedScrollPosition()
	{
		float interpolationFactor = GetAnimatingScrollInterpolationFactor();
		return m_currentScrollPos * interpolationFactor + m_previousScrollPos * (1.0f - interpolationFactor);
	}

	/******************************************************************
	*                                                                 *
	*  ListViewApp::OnResize                                          *
	*                                                                 *
	*  If the application receives a WM_SIZE message, this method     *
	*  resizes the render target appropriately.                       *
	*                                                                 *
	******************************************************************/
	void OnResize()
	{
		if (m_pRT is not null)
		{
			D2D_SIZE_U size = CalculateD2DWindowSize();
			_ = MoveWindow(m_d2dHwnd, 0, 0, (int)size.width, (int)size.height, false);

			// Note: This method can fail, but it's okay to ignore the
			// error here -- it will be repeated on the next call to
			// EndDraw.
			m_pRT.Resize(size);

			m_scrollRange = (msc_lineSpacing + msc_iconSize) * m_numItemInfos - msc_lineSpacing;

			SCROLLINFO si = new(SIF.SIF_DISABLENOSCROLL | SIF.SIF_PAGE | SIF.SIF_RANGE)
			{
				nMax = (int)m_scrollRange,
				nPage = size.height
			};
			_ = SetScrollInfo(Handle, SB.SB_VERT, si, true);

			_ = InvalidateRect(m_d2dHwnd, default, false);
		}
	}


	/******************************************************************
	*                                                                 *
	*  ListViewApp::CompareAToZ (static)                              *
	*                                                                 *
	*  A comparator function for sorting ItemInfos alphabetically     *
	*                                                                 *
	******************************************************************/
	static int CompareAToZ(ItemInfo a, ItemInfo b) => string.Compare(a.szFilename, b.szFilename);


	/******************************************************************
	*                                                                 *
	*  ListViewApp::CompareZToA (static)                              *
	*                                                                 *
	*  A comparator function for sorting ItemInfos in reverse         *
	*  alphabetical order.                                            *
	*                                                                 *
	******************************************************************/
	static int CompareZToA(ItemInfo a, ItemInfo b) => string.Compare(b.szFilename, a.szFilename);

	/******************************************************************
	*                                                                 *
	*  ListViewApp::CompareDirFirstAToZ (static)                      *
	*                                                                 *
	*  A comparator function for sorting ItemInfos in alphabetical    *
	*  order, with all directories before all other files.            *
	*                                                                 *
	******************************************************************/
	static int CompareDirFirstAToZ(ItemInfo a, ItemInfo b)
	{
		if (a.isDirectory && !b.isDirectory)
		{
			return -1;
		}
		else if (!a.isDirectory && b.isDirectory)
		{
			return 1;
		}
		else
		{
			return string.Compare(a.szFilename, b.szFilename);
		}
	}


	/******************************************************************
	*                                                                 *
	*  ListViewApp::OnChar                                            *
	*                                                                 *
	*  Called when the app receives a WM_CHAR message (which happens  *
	*  when a key is pressed).                                        *
	*                                                                 *
	******************************************************************/
	void OnChar(char aChar)
	{
		// We only do stuff for 'a', 'z', or 'd'
		if (aChar is 'a' or 'z' or 'd')
		{
			Comparison<ItemInfo> comparator = aChar switch
			{
				// 'a' means alphabetical sort
				'a' => CompareAToZ,
				// 'z' means reverse alphabetical sort
				'z' => CompareZToA,
				// 'd' means alphabetical sort, directories first
				_ => CompareDirFirstAToZ,
			};

			// Freeze file position to the current interpolated position so that
			// when we animate to the new positions, the items don't jump back to
			// their previous position momentarily.
			for (uint i = 0; i < m_numItemInfos; i++)
			{
				float interpolationFactor = GetAnimatingItemInterpolationFactor();
				m_pFiles[i].previousPosition = GetFancyAccelerationInterpolatedValue(interpolationFactor, m_pFiles[i].previousPosition, m_pFiles[i].currentPosition);
			}

			// Apply the new sort.
			Array.Sort(m_pFiles, comparator);

			// Set the new positions based up on the position of each item within
			// the sorted array.
			for (uint i = 0; i < m_numItemInfos; i++)
			{
				m_pFiles[i].currentPosition = (float)(i * (msc_iconSize + msc_lineSpacing));
			}

			// Animate the items to their new positions.
			m_animatingItems = msc_totalAnimatingItemFrames;
			_ = InvalidateRect(m_d2dHwnd, default, false);
		}
	}

	/******************************************************************
	*                                                                 *
	*  ListViewApp::GetScrolledDIPositionFromPixelPosition            *
	*                                                                 *
	*  Translate a pixel position to a position within our document,  *
	*  taking scrolling into account.                                 *
	*                                                                 *
	******************************************************************/
	D2D_POINT_2F GetScrolledDIPositionFromPixelPosition(D2D_POINT_2U pixelPosition)
	{
		D2D_POINT_2F dpi = default;
		m_pRT!.GetDpi(out dpi.x, out dpi.y);

		return Point2F(pixelPosition.x * 96 / dpi.x, pixelPosition.y * 96 / dpi.y + GetInterpolatedScrollPosition());
	}

	/******************************************************************
	*                                                                 *
	*  ListViewApp::ParentWndProc                                     *
	*                                                                 *
	*  Window message handler                                         *
	*                                                                 *
	******************************************************************/
	protected override nint WndProc(HWND hwnd, uint message, nint wParam, nint lParam)
	{
		switch ((WindowMessage)message)
		{
			case WindowMessage.WM_SIZE:
				OnResize();
				return 0;

			case WindowMessage.WM_VSCROLL:
				_ = HANDLE_WM_VSCROLL(hwnd, wParam, lParam, OnVScroll);
				return 0;

			case WindowMessage.WM_MOUSEWHEEL:
				_ = HANDLE_WM_MOUSEWHEEL(hwnd, wParam, lParam, OnMouseWheel);
				return 0;

			case WindowMessage.WM_CHAR:
				OnChar((char)LOWORD(wParam));
				return 0;

			default:
				return base.WndProc(hwnd, message, wParam, lParam);
		}
	}

	/******************************************************************
	*                                                                 *
	*  ListViewApp::ChildWndProc                                      *
	*                                                                 *
	*  Window message handler for the Child D2D window                *
	*                                                                 *
	******************************************************************/
	IntPtr ChildWndProc(HWND hwnd, uint message, IntPtr wParam, IntPtr lParam)
	{
		switch ((WindowMessage)message)
		{
			case WindowMessage.WM_PAINT:
			case WindowMessage.WM_DISPLAYCHANGE:
				_ = BeginPaint(hwnd, out var ps);
				OnRender();
				_ = EndPaint(hwnd, ps);
				return 0;

			case WindowMessage.WM_LBUTTONDOWN:
				D2D_POINT_2F diPosition = GetScrolledDIPositionFromPixelPosition(Point2U(LOWORD(lParam), HIWORD(lParam)));
				OnLeftButtonDown(diPosition);
				return 0;

			case WindowMessage.WM_DESTROY:
				PostQuitMessage(0);
				return 1;

			default:
				return DefWindowProc(hwnd, message, wParam, lParam);
		}
	}

	/******************************************************************
	*                                                                 *
	*  ListViewApp::OnLeftButtonDown                                  *
	*                                                                 *
	*  Called when the left mouse button is pressed inside the child  *
	*  D2D window                                                     *
	*                                                                 *
	******************************************************************/
	void OnLeftButtonDown(D2D_POINT_2F diPosition)
	{
		int index = (int)(diPosition.y / (msc_iconSize + msc_lineSpacing));
		if (index >= 0 && index < (int)(m_numItemInfos))
		{
			// Only process the click if the item isn't animating
			if (m_pFiles[index].currentPosition == m_pFiles[index].previousPosition)
			{
				if (diPosition.y < m_pFiles[index].currentPosition + msc_iconSize)
				{
					if (SetCurrentDirectory(m_pFiles[index].szFilename))
					{
						LoadDirectory();
						_ = InvalidateRect(m_d2dHwnd, default, false);
					}
				}
			}
		}
	}

	/******************************************************************
	*                                                                 *
	*  ListViewApp::OnLeftButtonDown                                  *
	*                                                                 *
	*  Called when the mouse wheel is moved.                          *
	*                                                                 *
	******************************************************************/
	void OnMouseWheel(HWND hwnd, MOUSEWHEEL mw, POINTS pt)
	{
		m_previousScrollPos = (int)(GetInterpolatedScrollPosition());
		m_currentScrollPos -= mw.distance;
		m_currentScrollPos = Math.Max(0, Math.Min(m_currentScrollPos, (int)(m_scrollRange) - (int)(m_pRT!.GetSize().height)));

		SCROLLINFO si = new(SIF.SIF_PAGE | SIF.SIF_POS | SIF.SIF_RANGE | SIF.SIF_TRACKPOS);
		bool ret = GetScrollInfo(Handle, SB.SB_VERT, ref si);
		if (!ret)
		{
			Debug.Assert(ret);
			return;
		}

		if (m_currentScrollPos != si.nPos)
		{
			si.nPos = m_currentScrollPos;
			_ = SetScrollInfo(Handle, SB.SB_VERT, si, true);

			m_animatingScroll = msc_totalAnimatingScrollFrames;
			_ = InvalidateRect(m_d2dHwnd, default, false);
		}
	}

	/******************************************************************
	*                                                                 *
	*  ListViewApp::OnVScroll                                         *
	*                                                                 *
	*  Called when a WM_VSCROLL message is sent.                      *
	*                                                                 *
	******************************************************************/
	void OnVScroll(HWND hwnd, HWND hwndCtl, SBCMD code, int pos)
	{
		int newScrollPos = m_currentScrollPos;

		switch (code)
		{
			case SBCMD.SB_LINEUP:
				newScrollPos -= 1;
				break;

			case SBCMD.SB_LINEDOWN:
				newScrollPos += 1;
				break;

			case SBCMD.SB_PAGEUP:
				newScrollPos -= (int)(m_pRT!.GetSize().height);
				break;

			case SBCMD.SB_PAGEDOWN:
				newScrollPos += (int)(m_pRT!.GetSize().height);
				break;

			case SBCMD.SB_THUMBTRACK:
				{
					if (!GetSI(out var tsi))
						return;
					newScrollPos = tsi.nTrackPos;
				}
				break;

			default:
				break;
		}

		newScrollPos = Math.Max(0, Math.Min(newScrollPos, (int)(m_scrollRange)));

		m_previousScrollPos = (int)(GetInterpolatedScrollPosition());

		m_currentScrollPos = newScrollPos;

		bool flowControl = GetSI(out var si);
		if (!flowControl)
			return;

		if (m_currentScrollPos != si.nPos)
		{
			si.nPos = m_currentScrollPos;
			_ = SetScrollInfo(Handle, SB.SB_VERT, si, true);

			m_animatingScroll = msc_totalAnimatingScrollFrames;
			_ = InvalidateRect(m_d2dHwnd, default, false);
		}

		bool GetSI(out SCROLLINFO si)
		{
			si = new(SIF.SIF_PAGE | SIF.SIF_POS | SIF.SIF_RANGE | SIF.SIF_TRACKPOS);
			bool ret = GetScrollInfo(Handle, SB.SB_VERT, ref si);
			if (!ret)
				Debug.Assert(ret);
			return ret;
		}
	}

	private static class DPIScale
	{
		private static float scale;
		static DPIScale() => Update();
		public static void Update() => scale = GetDpiForWindow(GetDesktopWindow()) / 96.0f;
		public static SIZE Scale(int width, int height) => new((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale));
	}

	class ItemInfo
	{
		public D2D_RECT_U placement = default;
		public string szFilename = "";
		public float currentPosition;
		public float previousPosition;
		public bool isDirectory;
	}
}