using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using WinUIClassicSamplesBrowser.Activation;
using WinUIClassicSamplesBrowser.Contracts.Services;
using WinUIClassicSamplesBrowser.Helpers;
using WinUIClassicSamplesBrowser.Models;
using WinUIClassicSamplesBrowser.Services;
using WinUIClassicSamplesBrowser.ViewModels;
using WinUIClassicSamplesBrowser.Views;
using WinUIEx;

namespace WinUIClassicSamplesBrowser;

// To learn more about WinUI 3, see https://docs.microsoft.com/windows/apps/winui/winui3/.
public partial class App : Application
{
    // The .NET Generic Host provides dependency injection, configuration, logging, and other services.
    // https://docs.microsoft.com/dotnet/core/extensions/generic-host
    // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
    // https://docs.microsoft.com/dotnet/core/extensions/configuration
    // https://docs.microsoft.com/dotnet/core/extensions/logging
    public IHost Host
    {
        get;
    }

    public static T GetService<T>()
        where T : class
    {
        if ((Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
        {
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
        }

        return service;
    }

    public static Window MainWindow { get; } = new MainWindow();

    public static UIElement? AppTitlebar { get; set; }

    public App()
    {
        InitializeComponent();

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder().
            UseContentRoot(AppContext.BaseDirectory).
            ConfigureServices((context, services) =>
        {
            // Default Activation Handler
            services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();

            // Other Activation Handlers
            services.AddTransient<IActivationHandler, AppNotificationActivationHandler>();

            // Services
            services.AddSingleton<IAppNotificationService, AppNotificationService>();
            services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
            services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
            services.AddTransient<IWebViewService, WebViewService>();
            services.AddTransient<INavigationViewService, NavigationViewService>();

            services.AddSingleton<IActivationService, ActivationService>();
            services.AddSingleton<IPageService, PageService>();
            services.AddSingleton<INavigationService, NavigationService>();

            // Core Services
            services.AddSingleton<IFileService, FileService>();

            // Views and ViewModels
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<SettingsPage>();
            services.AddTransient<WebBrowserViewModel>();
            services.AddTransient<WebBrowserPage>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<MainPage>();
            services.AddTransient<ShellPage>();
            services.AddTransient<ShellViewModel>();

            // Configuration
            services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));
        }).
        Build();

        GetService<IAppNotificationService>().Initialize();

        UnhandledException += App_UnhandledException;
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // TODO: Log and handle exceptions as appropriate.
        // https://docs.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.application.unhandledexception.
        // For more info see https://docs.microsoft.com/windows/apps/windows-app-sdk/api/winrt/microsoft.ui.xaml.unhandledexceptioneventargs.

/* BUG: `Class not registered (0x80040154 (REGDB_E_CLASSNOTREG))`
   ```
   20:58:57:210 Exception thrown: 'System.Runtime.InteropServices.COMException' in System.Private.CoreLib.dll
   20:58:57:210 An unhandled exception of type 'System.Runtime.InteropServices.COMException' occurred in System.Private.CoreLib.dll
   20:58:57:210 Class not registered (0x80040154 (REGDB_E_CLASSNOTREG))
   20:58:57:210 
   20:59:42:553 The program '[7164] WinUIClassicSamplesBrowser.exe' has exited with code 4294967295 (0xffffffff).```
   ```
   20:58:55:053 'WinUIClassicSamplesBrowser.exe' (CoreCLR: DefaultDomain): Loaded 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.25\System.Private.CoreLib.dll'. Symbol loading disabled by Include/Exclude setting.
   20:58:55:803 'WinUIClassicSamplesBrowser.exe' (CoreCLR: clrhost): Loaded 'D:\gitSource\WinUI Classic Samples Browser\WinUIClassicSamplesBrowser\bin\x64\Debug\net8.0-windows10.0.22621.0\WinUIClassicSamplesBrowser.dll'. Symbols loaded.
   20:58:56:056 'WinUIClassicSamplesBrowser.exe' (CoreCLR: clrhost): Loaded 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.25\System.Runtime.dll'. Symbol loading disabled by Include/Exclude setting.
   20:58:56:056 'WinUIClassicSamplesBrowser.exe' (CoreCLR: clrhost): Loaded 'c:\program files\microsoft visual studio\18\insiders\common7\ide\extensions\microsoft\managedprojectsystem\HotReload\net6.0\Microsoft.Extensions.DotNetDeltaApplier.dll'. 
   20:58:56:056 'WinUIClassicSamplesBrowser.exe' (CoreCLR: clrhost): Loaded 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.25\System.Linq.dll'. Symbol loading disabled by Include/Exclude setting.
   20:58:56:056 'WinUIClassicSamplesBrowser.exe' (CoreCLR: clrhost): Loaded 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.25\System.Runtime.Loader.dll'. Symbol loading disabled by Include/Exclude setting.
   20:58:56:056 'WinUIClassicSamplesBrowser.exe' (CoreCLR: clrhost): Loaded 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.25\System.Console.dll'. Symbol loading disabled by Include/Exclude setting.
   20:58:56:056 'WinUIClassicSamplesBrowser.exe' (CoreCLR: clrhost): Loaded 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.25\System.Collections.dll'. Symbol loading disabled by Include/Exclude setting.
   20:58:56:056 'WinUIClassicSamplesBrowser.exe' (CoreCLR: clrhost): Loaded 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.25\System.Runtime.InteropServices.RuntimeInformation.dll'. Symbol loading disabled by Include/Exclude setting.
   20:58:56:056 'WinUIClassicSamplesBrowser.exe' (CoreCLR: clrhost): Loaded 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.25\System.Collections.Concurrent.dll'. Symbol loading disabled by Include/Exclude setting.
   20:58:56:056 'WinUIClassicSamplesBrowser.exe' (CoreCLR: clrhost): Loaded 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.25\System.Threading.dll'. Symbol loading disabled by Include/Exclude setting.
   20:58:56:056 'WinUIClassicSamplesBrowser.exe' (CoreCLR: clrhost): Loaded 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.25\System.IO.Pipes.dll'. Symbol loading disabled by Include/Exclude setting.
   20:58:56:056 'WinUIClassicSamplesBrowser.exe' (CoreCLR: clrhost): Loaded 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.25\System.Runtime.InteropServices.dll'. Symbol loading disabled by Include/Exclude setting.
   20:58:56:056 'WinUIClassicSamplesBrowser.exe' (CoreCLR: clrhost): Loaded 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.25\System.Threading.Overlapped.dll'. Symbol loading disabled by Include/Exclude setting.
   20:58:56:056 'WinUIClassicSamplesBrowser.exe' (CoreCLR: clrhost): Loaded 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.25\System.Security.AccessControl.dll'. Symbol loading disabled by Include/Exclude setting.
   20:58:56:056 'WinUIClassicSamplesBrowser.exe' (CoreCLR: clrhost): Loaded 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.25\System.Security.Principal.Windows.dll'. Symbol loading disabled by Include/Exclude setting.
   20:58:56:056 'WinUIClassicSamplesBrowser.exe' (CoreCLR: clrhost): Loaded 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.25\System.Security.Claims.dll'. Symbol loading disabled by Include/Exclude setting.
   20:58:56:306 'WinUIClassicSamplesBrowser.exe' (CoreCLR: clrhost): Loaded 'D:\gitSource\WinUI Classic Samples Browser\WinUIClassicSamplesBrowser\bin\x64\Debug\net8.0-windows10.0.22621.0\WinRT.Runtime.dll'. Symbol loading disabled by Include/Exclude setting.
   20:58:57:210 'WinUIClassicSamplesBrowser.exe' (CoreCLR: clrhost): Loaded 'D:\gitSource\WinUI Classic Samples Browser\WinUIClassicSamplesBrowser\bin\x64\Debug\net8.0-windows10.0.22621.0\Microsoft.WinUI.dll'. Symbol loading disabled by Include/Exclude setting.
   20:58:57:210 'WinUIClassicSamplesBrowser.exe' (CoreCLR: clrhost): Loaded 'C:\Program Files\Microsoft Visual Studio\18\Insiders\Common7\IDE\PrivateAssemblies\Runtime\Microsoft.VisualStudio.Debugger.Runtime.NetCoreApp.dll'. 
   20:58:57:210 'WinUIClassicSamplesBrowser.exe' (CoreCLR: clrhost): Loaded 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.25\System.Memory.dll'. Symbol loading disabled by Include/Exclude setting.
   20:58:57:210 'WinUIClassicSamplesBrowser.exe' (CoreCLR: clrhost): Loaded 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.25\System.Runtime.CompilerServices.Unsafe.dll'. Symbol loading disabled by Include/Exclude setting.
   20:58:57:210 Exception thrown: 'System.Runtime.InteropServices.COMException' in System.Private.CoreLib.dll
   20:58:57:210 An unhandled exception of type 'System.Runtime.InteropServices.COMException' occurred in System.Private.CoreLib.dll
   20:58:57:210 Class not registered (0x80040154 (REGDB_E_CLASSNOTREG))
   20:59:42:553 The program '[7164] WinUIClassicSamplesBrowser.exe' has exited with code 4294967295 (0xffffffff).```
 */
    }

    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        GetService<IAppNotificationService>().Show(string.Format("AppNotificationSamplePayload".GetLocalized(), AppContext.BaseDirectory));

        await GetService<IActivationService>().ActivateAsync(args);
    }
}
