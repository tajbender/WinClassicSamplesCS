using ClassicSamplesBrowser.Helpers;
using ClassicSamplesBrowser.Views;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using WinRT;

namespace ClassicSamplesBrowser;

public sealed partial class MainWindow : Window
{
    private WindowsSystemDispatcherQueueHelper _wsdqHelper;
    private MicaController _micaController;
    private SystemBackdropConfiguration _backdropConfig;

    public MainWindow()
    {
        InitializeComponent();
        TrySetMicaBackdrop();

        RootFrame.Navigate(typeof(MainPage));
    }

    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (_backdropConfig != null)
        {
            _backdropConfig.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }
    }

    private bool TrySetMicaBackdrop()
    {
        if (!MicaController.IsSupported())
            return false;

        _wsdqHelper = new WindowsSystemDispatcherQueueHelper();
        _wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

        _backdropConfig = new SystemBackdropConfiguration
        {
            IsInputActive = true,
            Theme = SystemBackdropTheme.Default
        };

        _micaController = new MicaController
        {
            Kind = MicaKind.BaseAlt
        };

        _micaController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
        _micaController.SetSystemBackdropConfiguration(_backdropConfig);

        return true;
    }
}