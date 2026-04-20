using Microsoft.UI.Xaml;
using System.Diagnostics;
//using LaunchActivatedEventArgs = Windows.ApplicationModel.Activation.LaunchActivatedEventArgs;

namespace ClassicSamplesBrowser;

public partial class App : Application
{
    public static string WinAppSdkVersionInfo { get; } = Microsoft.WindowsAppSDK.Release.FormattedVersionTag;

    public MainWindow MainWindow { get; }

    public App()
    {
        try
        {
            this.InitializeComponent();
            MainWindow = new MainWindow();
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
        try
        {
            return Microsoft.WindowsAppSDK.Release.FormattedVersionTag ?? $"unknown."; ;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return $"Error fetching version: `{e.Message}`. hResult = `{e.HResult}`.";  // throw;
        }
    }
}
