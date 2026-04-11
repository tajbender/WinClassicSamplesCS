using Microsoft.UI.Xaml;
//using LaunchActivatedEventArgs = Windows.ApplicationModel.Activation.LaunchActivatedEventArgs;

namespace ClassicSamplesBrowser;

public partial class App : Application
{
    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new MainWindow();
        window.Activate();
    }
}
