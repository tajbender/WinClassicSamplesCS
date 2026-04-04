using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.UI.ViewManagement;
using WinRT;
using WinUIClassicSamplesBrowser.Helpers;

namespace WinUIClassicSamplesBrowser;

public sealed partial class MainWindow : Window
{
    private readonly SystemBackdropConfiguration? _configuration = new();
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private readonly MicaController? _micaController;
    private readonly UISettings _settings = new();

    public MainWindow()
    {
        InitializeComponent();

        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets/WindowIcon.ico"));
        Content = null;
        Title = "AppDisplayName".GetLocalized();

        // Theme change code picked from https://github.com/microsoft/WinUI-Gallery/pull/1239
        // info: cannot use FrameworkElement.ActualThemeChanged event
        _settings.ColorValuesChanged += Settings_ColorValuesChanged;

        // Mica Backdrop handling
        _micaController = new MicaController();
        _micaController.Kind = MicaKind.BaseAlt;
        _micaController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
        _micaController.SetSystemBackdropConfiguration(_configuration);
    }

    public bool TrySetMicaBackdrop()
    {
        if (!MicaController.IsSupported())
            return false;

        try
        {
            this._configuration.IsInputActive = true;
            this._configuration.Theme = SystemBackdropTheme.Default;

//            _micaController = new MicaController();
//            _micaController.Kind = MicaKind.BaseAlt;
//            _micaController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
//            _micaController.SetSystemBackdropConfiguration(_configuration);

            return true;
        }
        catch (COMException comEx)
        {
            Debug.Fail(comEx.Message, $"hResult<{comEx.HResult}>");
            throw;
        }
        catch (Exception ex)
        {
            Debug.Fail(ex.Message, ex.HelpLink);
            throw;
        }
    }

    // this handles updating the caption button colors correctly when indows system theme is changed
    // while the app is open
    private void Settings_ColorValuesChanged(UISettings sender, object args)
    {
        // This calls comes off-thread, hence we will need to dispatch it to current app's thread
        _dispatcherQueue.TryEnqueue(() =>
        {
            TitleBarHelper.ApplySystemThemeToCaptionButtons();
        });
    }
}
