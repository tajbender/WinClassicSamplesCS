using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Windows.UI.ViewManagement;
using WinRT;

using WinUIClassicSamplesBrowser.Helpers;

namespace WinUIClassicSamplesBrowser;

public sealed partial class MainWindow : WindowEx
{
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

    private readonly UISettings _settings = new();

    MicaController? micaController;
    SystemBackdropConfiguration? configuration;

    public MainWindow()
    {
        InitializeComponent();

        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets/WindowIcon.ico"));
        Content = null;
        Title = "AppDisplayName".GetLocalized();

        // Theme change code picked from https://github.com/microsoft/WinUI-Gallery/pull/1239
        _settings.ColorValuesChanged += Settings_ColorValuesChanged; // cannot use FrameworkElement.ActualThemeChanged event
    }

    public bool TrySetMicaBackdrop()
    {
        if (!MicaController.IsSupported())
            return false;

        try
        {
            var configuration = new SystemBackdropConfiguration
            {
                IsInputActive = true,
                Theme = SystemBackdropTheme.Default
            };

            micaController = new MicaController();
            micaController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
            micaController.SetSystemBackdropConfiguration(configuration);

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
