using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Composition.SystemBackdrops;
using ClassicSamplesBrowser.Helpers;
using ClassicSamplesBrowser.Views;
using WinRT;

namespace ClassicSamplesBrowser;

public sealed partial class MainWindow : Window
{
    private MicaController _micaController = new();
    private SystemBackdropConfiguration _sysBackdropConfiguration = new();
    private WindowsSystemDispatcherQueueHelper _winDispatcherHelper = new();

    public MainWindow()
    {
        try
        {
            int charsWritten;
            Span<char> buffer = stackalloc char[256];

            InitializeComponent();
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
}
