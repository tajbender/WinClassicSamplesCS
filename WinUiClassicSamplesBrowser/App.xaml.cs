using Microsoft.UI.Xaml;
using System.Diagnostics;
//using LaunchActivatedEventArgs = Windows.ApplicationModel.Activation.LaunchActivatedEventArgs;

namespace ClassicSamplesBrowser;

public partial class App : Application
{
    private readonly MainWindow _mainWindow;

    public static string WinAppSdkVersionInfo { get; } = Microsoft.WindowsAppSDK.Release.FormattedVersionTag;

    public MainWindow MainWindow => _mainWindow;

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

    //protected override void OnLaunched(LaunchActivatedEventArgs args)
    //{
    //    try
    //    {
    //        _mainWindow.Activate();
    //    }
    //    catch (Exception e)
    //    {
    //        Console.WriteLine(e);
    //        throw;
    //    }
    //}

    private static string GetWinAppSdkFormattedVersionTag()
    {
        string version = "{unknown.}";

        try
        {
            // Use the version information as needed
            version = Microsoft.WindowsAppSDK.Release.FormattedVersionTag;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
//        var formattedVersionTag = Microsoft.WindowsAppSDK.Release.FormattedVersionTag;
//        var version = formattedVersionTag;
//
        return version;
    }
}
