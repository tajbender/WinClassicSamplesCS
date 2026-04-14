using ClassicSamplesBrowser.Helpers;
using ClassicSamplesBrowser.Views;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.WinUI;
using Vortice.WinUI.Composition;
using Windows.UI.Xaml.Interop;
using WinRT;
using static Vortice.Direct3D11.D3D11;


namespace ClassicSamplesBrowser;

public sealed partial class MainWindow : Window
{
    private MicaController _micaController = new();
    private SystemBackdropConfiguration _sysBackdropConfiguration = new();
    private WindowsSystemDispatcherQueueHelper _winDispatcherHelper = new();

    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDXGISwapChain1? _swapChain;
    private ID3D11RenderTargetView? _rtv;
    private DispatcherTimer? _timer;
    private float _angle;

    public MainWindow()
    {
        try
        {
            int charsWritten;
            Span<char> buffer = stackalloc char[256];

            InitializeComponent();
            InitializeDirectX();
            Debug.WriteLine(TrySetMicaBackdrop().TryFormat(buffer, out charsWritten));
        }
        catch
        {
            // If Mica is not supported, we don't want to crash the app, so we catch all exceptions and continue without setting the backdrop.
            throw;
        }

        try
        {
            var result = RootFrame.Navigate(typeof(StartPage));
        }
        catch
        {
            // If navigation fails, we don't want to crash the app, so we catch all exceptions and continue without navigating.
            throw;
        }
        finally
        {
            Debug.Print("MainWindow.ctor() has initially navigated successfully.");
        }
    }

    internal void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        _sysBackdropConfiguration.IsInputActive = (args.WindowActivationState != WindowActivationState.Deactivated);
    }

    private bool TrySetMicaBackdrop()
    {
        if (!MicaController.IsSupported())
            return false;

        _winDispatcherHelper = new();
        _winDispatcherHelper.EnsureWindowsSystemDispatcherQueueController();

        _sysBackdropConfiguration = new()
        {
            IsInputActive = true,
            Theme = SystemBackdropTheme.Default
        };

        _micaController = new()
        {
            Kind = MicaKind.BaseAlt
        };

        _micaController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
        _micaController.SetSystemBackdropConfiguration(_sysBackdropConfiguration);

        return true;
    }

    private void InitializeDirectX()
    {
        // Device + Context
        D3D11CreateDevice(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            new[] { FeatureLevel.Level_11_0 },
            out _device,
            out _,
            out _context);

        // SwapChain-Desc
        var swapDesc = new SwapChainDescription1
        {
            Width = 0,
            Height = 0,
            Format = Format.B8G8R8A8_UNorm,
            BufferUsage = Usage.RenderTargetOutput,
            BufferCount = 2,
            SampleDescription = new SampleDescription(1, 0),
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipDiscard,
        };

        using var dxgiDevice = _device!.QueryInterface<IDXGIDevice>();
        using var adapter = dxgiDevice.GetAdapter();
        using var factory = adapter.GetParent<IDXGIFactory2>();

        // Retrieve `SwapChainPanelNative`
        //ISwapChainPanelNative nativeSwapChainPanel = GetSwapChainPanelNative(SwapChainHost);
        //nativeSwapChainPanel?.SetSwapChain(_swapChain);
        //native.SetSwapChain(_swapChain);
        //factory.CreateSwapChainForComposition(_device, ref swapDesc, null, out _swapChain);
        //panelNative.SetSwapChain(_swapChain!);
        //CreateRenderTarget();
    }

    //private ISwapChainPanelNative GetSwapChainPanelNative(SwapChainPanel swapChainHost)
    //{
    //    return null;
    //}

    private void CreateRenderTarget()
    {
        try
        {
            using var backBuffer = _swapChain!.GetBuffer<ID3D11Texture2D>(0);
            _rtv = _device!.CreateRenderTargetView(backBuffer);
        }
        catch(Exception ex)
        {
            Debug.Print($"CreateRenderTarget() Failed to create render target: {ex.Message}");
        }
        finally
        {
            Debug.Print("CreateRenderTarget() has completed.");
        }
    }
}
