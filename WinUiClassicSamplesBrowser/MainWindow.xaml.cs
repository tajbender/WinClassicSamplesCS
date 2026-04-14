using Microsoft.UI.Xaml;
using Microsoft.UI.Composition.SystemBackdrops;
using ClassicSamplesBrowser.Helpers;
using ClassicSamplesBrowser.Views;
using WinRT;

namespace ClassicSamplesBrowser;

public sealed partial class MainWindow : Window
{
    private MicaController _micaController = new();
    private SystemBackdropConfiguration _sysBackdropConfiguration;
    private WindowsSystemDispatcherQueueHelper _winDispatcherHelper =new();

    public MainWindow()
    {
        InitializeComponent();
        TrySetMicaBackdrop();

        RootFrame.Navigate(typeof(MainPage));
    }

    private void Window_Activated(object sender, WindowActivatedEventArgs args)
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
}
