using System.Runtime.Versioning;
using Vanara.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.D2d1;
using static Vanara.PInvoke.D3D11;
using static Vanara.PInvoke.Dcomp;
using static Vanara.PInvoke.DXGI;
using static Vanara.PInvoke.Gdi32;
using static Vanara.PInvoke.UIAnimation;
using static Vanara.PInvoke.User32;
using static Vanara.PInvoke.WindowsCodecs;
using HRESULT = Vanara.PInvoke.HRESULT;

namespace DirectComposition_WAM;

[SupportedOSPlatform("windows8.0")]
internal class Program
{
	public static void Main() => VisibleWindow.Run<CApplication>("Hello");
}

[SupportedOSPlatform("windows8.0")]
public class CApplication : VisibleWindow
{
	private const int WINDOW_SIZE = 500;
	private const float TILE_SPACING = 170.0f;

	private IUIAnimationManager2 _manager;
	private IUIAnimationTransitionLibrary2 _transitionLibrary;
	private IUIAnimationVariable2 _animationVariable;
	private ID3D11Device _d3d11Device;
	private ID3D11DeviceContext _d3d11DeviceContext;
	private string _fontTypeface;
	private int _fontHeightLogo;
	private int _fontHeightTitle;
	private int _fontHeightDescription;
	private ID2D1Factory1 _d2d1Factory;
	private ID2D1Device _d2d1Device;
	private ID2D1DeviceContext _d2d1DeviceContext;
	private IDCompositionDevice _device;
	private IDCompositionTarget _target;
	private IDCompositionVisual _visual;
	private int _bitmapWidth;
	private int _bitmapHeight;
	private IDCompositionVisual[] _visualChild = new IDCompositionVisual[30];
	private SafeHBRUSH _hbrush;

	private enum DIRECTION
	{
		stopForward = -1,
		stopBackward = 1,
		forward,
		backward,
	}

	//-------------------------------------------------------
	// Creates and initializes all the objects we need for the application
	//--------------------------------------------------------

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
	public CApplication()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
	{
	}

	protected override IntPtr WndProc(HWND hwnd, uint msg, IntPtr wParam, IntPtr lParam)
	{
		switch ((WindowMessage)msg)
		{
			case WindowMessage.WM_CREATE:
				OnCreate();
				break;

			case WindowMessage.WM_PAINT:
				return OnPaint(hwnd);

			case WindowMessage.WM_LBUTTONDOWN:
				return Move(DIRECTION.forward);

			case WindowMessage.WM_RBUTTONDOWN:
				return Move(DIRECTION.backward);

			case WindowMessage.WM_LBUTTONUP:
				return Move(DIRECTION.stopForward);

			case WindowMessage.WM_RBUTTONUP:
				return Move(DIRECTION.stopBackward);

			case WindowMessage.WM_CLOSE:
				_hbrush.Dispose();
				break;
		}
		return base.WndProc(hwnd, msg, wParam, lParam);
	}

	private HRESULT CreateD3D11Device()
	{
		D3D_DRIVER_TYPE[] driverTypes = [D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE, D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_WARP];
		for (int i = 0; i < driverTypes.Length; ++i)
		{
			if (D3D11CreateDevice(null, driverTypes[i], default, D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT, default, 0, D3D11_SDK_VERSION, out _d3d11Device, out _, out _d3d11DeviceContext).Succeeded)
				return HRESULT.S_OK;
		}
		return HRESULT.E_FAIL;
	}

	private HRESULT CreateD2D1Device() => WrapHR(() =>
	{
		if (_d3d11Device == null || _d2d1Factory == null)
			throw new InvalidOperationException();

		IDXGIDevice _dxgiDevice = (IDXGIDevice)_d3d11Device!;
		_d2d1Factory!.CreateDevice(_dxgiDevice!, out _d2d1Device);
		_d2d1DeviceContext = _d2d1Device.CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS.D2D1_DEVICE_CONTEXT_OPTIONS_NONE);
	});

	//-----------------------------------------------------------
	// Creates a DirectComposition device
	//-----------------------------------------------------------
	private HRESULT CreateDCompositionDevice()
	{
		if (_d3d11Device == null) return HRESULT.E_UNEXPECTED;

		IDXGIDevice? dxgiDevice = _d3d11Device! as IDXGIDevice;
		HRESULT hr = (int)DCompositionCreateDevice(dxgiDevice, out IDCompositionDevice? _device);

		return hr;
	}

	//--------------------------------------------------------
	// Creates an render target for DirectComposition which
	// is an hwnd in this case
	//--------------------------------------------------------
	private HRESULT CreateDCompositionRenderTarget()
	{
		if (_device == null || Handle.IsNull) return HRESULT.E_UNEXPECTED;

		return _device!.CreateTargetForHwnd(Handle, true, out _target);
	}

	//---------------------------------------------------------------------
	// Creates a DirectComposition visual tree and places each visual
	// inside the application window
	//---------------------------------------------------------------------
	private HRESULT CreateDCompositionVisualTree()
	{
		if (_device == null || Handle.IsNull) return HRESULT.E_UNEXPECTED;

		string filename = "220Strawberry.png";
		float tileSize = 0.3f * WINDOW_SIZE;
		int visualChildCount = _visualChild.Length;
		//float d = 2.0f * WINDOW_SIZE;

		// Create DirectComposition surface from the bitmap file
		var hr = CreateSurfaceFromFile(filename, out var bitmapWidth, out var bitmapHeight, out var surface);

		if (hr.Succeeded)
		{
			_bitmapWidth = bitmapWidth;
			_bitmapHeight = bitmapHeight;

			_visual = _device.CreateVisual();
		}

		// Set the content of each visual to be the surface that was created from the bitmap
		if (hr.Succeeded)
		{
			for (int i = 0; hr.Succeeded && i < visualChildCount; ++i)
			{
				_visualChild[i] = _device.CreateVisual();

				if (hr.Succeeded)
				{
					_visual.AddVisual(_visualChild[i], false, null);
				}

				if (hr.Succeeded)
				{
					_visualChild[i].SetContent(surface);
				}
			}
		}

		// Using DirectComposition transforms to scale and place each visual such that the tiles
		// are side by side within the application window
		if (hr.Succeeded)
		{
			for (int i = 0; hr.Succeeded && i < visualChildCount; ++i)
			{
				//setting up scale transform on each visual
				IDCompositionScaleTransform? scaleTransform = null;

				if (hr.Succeeded)
				{
					scaleTransform = _device.CreateScaleTransform();
				}

				float sx = tileSize / bitmapWidth;

				if (hr.Succeeded && scaleTransform is not null)
				{
					scaleTransform.SetScaleX(sx);
				}

				float sy = tileSize / bitmapHeight;

				if (hr.Succeeded && scaleTransform is not null)
				{
					scaleTransform.SetScaleY(sy);
				}

				//Setting up a translate transform on each visual
				IDCompositionTranslateTransform? translateTransform = null;

				if (hr.Succeeded)
				{
					translateTransform = _device.CreateTranslateTransform();
				}

				float x = (visualChildCount - 1 - i) * TILE_SPACING;
				float y = TILE_SPACING + 30;

				if (hr.Succeeded && translateTransform is not null)
				{
					translateTransform.SetOffsetX(x);
				}

				if (hr.Succeeded && translateTransform is not null)
				{
					translateTransform.SetOffsetY(y);
				}

				// Creating a transform group to group the two transforms together such that
				// they can be applied at once.
				IDCompositionTransform[] transforms = [scaleTransform!, translateTransform!];

				IDCompositionTransform? transformGroup = null;
				if (hr.Succeeded)
				{
					transformGroup = _device.CreateTransformGroup(transforms, (uint)transforms.Length);
				}
				if (hr.Succeeded && transformGroup is not null)
				{
					_visualChild[i].SetTransform(transformGroup);
				}
			}
		}

		return hr;
	}

	//-------------------------------------------------------------------------------
	// Use WAM to generate and propagate the appropriate animation curves to DirectComposition when
	// keypress is detected
	//-------------------------------------------------------------------------------
	private HRESULT CreateSlideAnimation(DIRECTION dir, out IDCompositionAnimation? slideAnimation)
	{
		float rightMargin = 27 * TILE_SPACING * -1; //where the tiles end. Note forward direction is represented by a negative value.
		float leftMargin = 0; // where the tiles begin

		slideAnimation = null;
		if (_device == null || _animationVariable == null) return HRESULT.E_UNEXPECTED;

		//Create a storyboard for the slide animation
		try
		{
			//WAM propagates curves to DirectComposition using the IDCompositionAnimation object
			var animation = _device.CreateAnimation();

			var storyboard = _manager.CreateStoryboard();

			// Synchronizing WAM and DirectComposition time such that when WAM Update is called,
			// the value reflects the DirectComposition value at the given time.
			var frameStatistics = _device.GetFrameStatistics();

			double nextEstimatedFrameTime = 0.0;

			nextEstimatedFrameTime = frameStatistics.nextEstimatedFrameTime / frameStatistics.timeFrequency;

			//Upating the WAM time
			_manager.Update(nextEstimatedFrameTime, out _);

			int velocity = 500; //arbitrary fix velocity for the slide animation

			var curValue = _animationVariable.GetValue();

			IUIAnimationTransition2? transition = null;
			switch (dir)
			{
				case DIRECTION.stopForward:
				case DIRECTION.stopBackward:
					// Stopping the animation smoothly when key is let go
					if (curValue != leftMargin && curValue != rightMargin)
						transition = _transitionLibrary.CreateSmoothStopTransition(0.5, curValue + (int)dir * 50.0);
					break;

				case DIRECTION.forward:
					// slide the tiles forward using a linear curve upon left button press
					transition = _transitionLibrary.CreateLinearTransition(-1 * (rightMargin - curValue) / velocity, rightMargin);
					break;

				case DIRECTION.backward:
					// slide the tiles backward using a linear cruve upon right button press
					transition = _transitionLibrary.CreateLinearTransition(-1 * curValue / velocity, leftMargin);
					break;
			}

			//Add above transition to storyboard
			if (transition is not null)
			{
				storyboard.AddTransition(_animationVariable, transition);

				//schedule the storyboard for play at the next estimate vblank
				storyboard.Schedule(nextEstimatedFrameTime, out _);

				//Giving WAM varialbe the IDCompositionAnimation object to recieve the animation curves
				_animationVariable.GetCurve(animation);

				slideAnimation = animation;
			}

			return HRESULT.S_OK;
		}
		catch (Exception ex)
		{
			return ex.HResult;
		}
	}

	private HRESULT WrapHR(Action action) { try { action(); return HRESULT.S_OK; } catch (Exception ex) { return ex.HResult; } }

	private HRESULT AttachDCompositionVisualTreeToRenderTarget() => _target == null || _visual == null ? (HRESULT)HRESULT.E_UNEXPECTED : WrapHR(() => _target.SetRoot(_visual));

	private HRESULT DetachDCompositionVisualTreeToRenderTarget() => _target == null ? (HRESULT)HRESULT.E_UNEXPECTED : WrapHR(() => _target.SetRoot(null));

	private IntPtr Move(DIRECTION dir)
	{
		if (_device == null || Handle.IsNull) return HRESULT.E_UNEXPECTED;

		try
		{
			// Create the animation curves using WAM
			CreateSlideAnimation(dir, out var slideAnimation).ThrowIfFailed();

			var translateTransform = _device.CreateTranslateTransform();

			//Set DirectComposition translation animation using the curves propagated by WAM
			translateTransform.SetOffsetX(slideAnimation ?? throw new InvalidOperationException());

			_visual.SetTransform(translateTransform);

			// Committing all changes to DirectComposition visuals in order for them to take effect visually
			_device.Commit();

			return IntPtr.Zero;
		}
		catch (Exception ex)
		{
			return ex.HResult;
		}
	}

	private IntPtr OnPaint(HWND hwnd)
	{
		using PaintContext pc = new(hwnd);
		FillRect(pc.hdc, pc.rcPaint, _hbrush);

		// get the dimensions of the main window.
		GetClientRect(Handle, out var rcClient);

		// Logo
		using (var hlogo = CreateFont(_fontHeightLogo, pszFaceName: _fontTypeface)) // Logo Font and Size
			if (!hlogo.IsNull)
			{
				using var sc = pc.hdc.SelectObject(hlogo);

				SetBkMode(pc.hdc, BackgroundMode.TRANSPARENT);

				rcClient.top = 10;
				rcClient.left = 30;

				DrawText(pc.hdc, "Windows samples", -1, rcClient, DrawTextFlags.DT_WORDBREAK);
			}

		// Title
		using (var htitle = CreateFont(_fontHeightTitle, pszFaceName: _fontTypeface)) // Title Font and Size
			if (!htitle.IsNull)
			{
				using var sc = pc.hdc.SelectObject(htitle);

				SetTextColor(pc.hdc, GetSysColor(SystemColorIndex.COLOR_WINDOWTEXT));

				rcClient.top = 25;
				rcClient.left = 30;

				DrawText(pc.hdc, "WAM Sample", -1, rcClient, DrawTextFlags.DT_WORDBREAK);
			}

		// Description
		using (var hdescription = CreateFont(_fontHeightDescription, pszFaceName: _fontTypeface)) // Description Font and Size
			if (!hdescription.IsNull)
			{
				using var sc = pc.hdc.SelectObject(hdescription);

				rcClient.top = 90;
				rcClient.left = 30;

				DrawText(pc.hdc, "This sample shows how DirectComposition and Windows Animation Manager (WAM) can be used together as an independent animation platform.", -1, rcClient, DrawTextFlags.DT_WORDBREAK);

				rcClient.top = 400;
				rcClient.left = 220;

				DrawText(pc.hdc, "Left/Right click to control the animation.", -1, rcClient, DrawTextFlags.DT_WORDBREAK);
			}

		return IntPtr.Zero;
	}

	private class PaintContext : IDisposable
	{
		private readonly HWND hwnd;
		private PAINTSTRUCT ps;

		public PaintContext(HWND hwnd) => hdc = new((IntPtr)BeginPaint(this.hwnd = hwnd, out ps), false);

		public SafeHDC hdc { get; }

		// Indicates whether the background must be erased. This value is nonzero if the application should erase the background. The
		// application is responsible for erasing the background if a window class is created without a background brush. For more
		// information, see the description of the hbrBackground member of the WNDCLASS structure.
		public bool fErase => ps.fErase;

		// Summary: A RECT structure that specifies the upper left and lower right corners of the rectangle in which the painting is
		// requested, in device units relative to the upper-left corner of the client area.
		public RECT rcPaint => ps.rcPaint;

		void IDisposable.Dispose() => EndPaint(hwnd, ps);
	}

	private void OnCreate()
	{
		_hbrush = CreateSolidBrush(new(255, 255, 255));
		try
		{
			CreateD3D11Device().ThrowIfFailed();
			_d2d1Factory = D2D1CreateFactory<ID2D1Factory1>(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED);
			CreateD2D1Device().ThrowIfFailed();
			_fontTypeface = Properties.Resources.IDS_FONT_TYPEFACE;
			_fontHeightLogo = int.Parse(Properties.Resources.IDS_FONT_HEIGHT_LOGO);
			_fontHeightTitle = int.Parse(Properties.Resources.IDS_FONT_HEIGHT_TITLE);
			_fontHeightDescription = int.Parse(Properties.Resources.IDS_FONT_HEIGHT_DESCRIPTION);
			_manager = new IUIAnimationManager2();
			_transitionLibrary = new IUIAnimationTransitionLibrary2();
			_animationVariable = _manager.CreateAnimationVariable(0.0);
			CreateDCompositionDevice().ThrowIfFailed();
			CreateDCompositionRenderTarget().ThrowIfFailed();
			CreateDCompositionVisualTree().ThrowIfFailed();
			AttachDCompositionVisualTreeToRenderTarget().ThrowIfFailed();
			_device!.Commit();
		}
		catch (Exception ex)
		{
			MessageBox(default, ex.Message, null, MB_FLAGS.MB_OK | MB_FLAGS.MB_ICONERROR);
			Environment.Exit(ex.HResult);
		}
	}

	private HRESULT CreateSurfaceFromFile(string filename, out int bitmapWidth, out int bitmapHeight, out IDCompositionSurface? surface)
	{
		bitmapWidth = bitmapHeight = 0;
		surface = default;

		if (filename == null) return HRESULT.E_INVALIDARG;

		try
		{
			CreateD2D1BitmapFromFile(filename, out var d2d1Bitmap).ThrowIfFailed();

			var bitmapSize = d2d1Bitmap!.GetSize();

			var surfaceTile = _device.CreateSurface((uint)bitmapSize.width, (uint)bitmapSize.height, DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM, DXGI_ALPHA_MODE.DXGI_ALPHA_MODE_IGNORE);

			RECT rect = new(0, 0, (int)bitmapSize.width, (int)bitmapSize.height);
			surfaceTile.BeginDraw(rect, out IDXGISurface? dxgiSurface, out var offset).ThrowIfFailed();

			bitmapWidth = (int)bitmapSize.width;
			bitmapHeight = (int)bitmapSize.height;
			surface = surfaceTile;

#pragma warning disable CS0618 // Type or member is obsolete
			_d2d1Factory.GetDesktopDpi(out var dpiX, out var dpiY);
#pragma warning restore CS0618 // Type or member is obsolete

			using SafeCoTaskMemStruct<D2D1_BITMAP_PROPERTIES1> bitmapProperties = new D2D1_BITMAP_PROPERTIES1()
			{
				bitmapOptions = D2D1_BITMAP_OPTIONS.D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS.D2D1_BITMAP_OPTIONS_CANNOT_DRAW,
				pixelFormat = new D2D1_PIXEL_FORMAT()
				{
					format = DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM,
					alphaMode = D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_IGNORE,
				},
				dpiX = dpiX,
				dpiY = dpiY
			};

			var d2d1Target = _d2d1DeviceContext.CreateBitmapFromDxgiSurface(dxgiSurface!, bitmapProperties);

			_d2d1DeviceContext.SetTarget(d2d1Target);

			_d2d1DeviceContext.BeginDraw();

			D2D_RECT_F prect = new(offset.x + 0.0f,
				offset.y + 0.0f,
				offset.x + bitmapSize.width,
				offset.y + bitmapSize.height);
			_d2d1DeviceContext.DrawBitmap(d2d1Bitmap!, destinationRectangle: prect);

			_d2d1DeviceContext.EndDraw(out _, out _);

			surfaceTile.EndDraw();

			return HRESULT.S_OK;
		}
		catch (Exception ex)
		{
			return ex.HResult;
		}
	}

	private HRESULT CreateD2D1BitmapFromFile(string filename, out ID2D1Bitmap? bitmap)
	{
		bitmap = null;

		try
		{
			var _wicFactory = new IWICImagingFactory();

			var wicBitmapDecoder = _wicFactory.CreateDecoderFromFilename(filename, SafeGuidPtr.Null, ACCESS_MASK.GENERIC_READ,
				WICDecodeOptions.WICDecodeMetadataCacheOnLoad);

			var wicBitmapFrame = wicBitmapDecoder.GetFrame(0);

			IWICFormatConverter wicFormatConverter = _wicFactory.CreateFormatConverter();

			Guid temp = WICGuids.GUID_WICPixelFormat32bppPBGRA;
			wicFormatConverter.Initialize(wicBitmapFrame, temp, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0, WICBitmapPaletteType.WICBitmapPaletteTypeMedianCut);

			var wicBitmap = _wicFactory.CreateBitmapFromSource(wicFormatConverter, WICBitmapCreateCacheOption.WICBitmapCacheOnLoad);

			bitmap = _d2d1DeviceContext.CreateBitmapFromWicBitmap(wicBitmap, default);

			return HRESULT.S_OK;
		}
		catch (Exception ex)
		{
			return ex.HResult;
		}
	}
}