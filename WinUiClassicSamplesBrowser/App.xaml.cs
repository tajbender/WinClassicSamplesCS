using System.Diagnostics;
using Microsoft.UI.Xaml;
//using LaunchActivatedEventArgs = Windows.ApplicationModel.Activation.LaunchActivatedEventArgs;

namespace ClassicSamplesBrowser;

public partial class App : Application
{
    private readonly MainWindow _mainWindow;

    public App()
    {
        try
        {
            this.InitializeComponent();
            _mainWindow = new MainWindow();
        }
        finally
        {
            Debug.Indent();
            Debug.WriteLine("Initialization complete.");
        }
    }

    /// <summary>
    /// 
    /// </summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _mainWindow.Activate();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    private void GetWinAppSDKVersionInfo()
    {
        // Use the version information as needed
        var formattedVersionTag = Microsoft.WindowsAppSDK.Release.FormattedVersionTag;
        var version = formattedVersionTag;
    }
}
