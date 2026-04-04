using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;

using WinUIClassicSamplesBrowser.Activation;
using WinUIClassicSamplesBrowser.Contracts.Services;
using WinUIClassicSamplesBrowser.Helpers;
using WinUIClassicSamplesBrowser.Models;
using WinUIClassicSamplesBrowser.Notifications;
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

    public static WindowEx MainWindow { get; } = new MainWindow();

    public static UIElement? AppTitlebar
    {
        get; set;
    }

    public App()
    {
        InitializeComponent();

        Host = Microsoft.Extensions.Hosting.Host.
        CreateDefaultBuilder().
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
        var guruMeditation = new StringBuilder();
        guruMeditation.AppendFormat($"System error {e.Message}");
        guruMeditation.AppendLine($"stack: {e.ToString()}");

        var comEx = e.Exception;
        var isComException = e.Exception.Equals((COMException)comEx);
        
        if (comEx != null)
        {
            if (comEx.HResult.Equals(0x80040154)) /* REGDB_E_CLASSNOTREG */
            {
            }
        }


        /** todo: handle this excaption: */
                /* System.Runtime.InteropServices.COMException
                    HResult=0x80040154
                    Message=Class not registered (0x80040154 (REGDB_E_CLASSNOTREG))
                    Source=System.Private.CoreLib
                    StackTrace:
                     at System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(Int32 errorCode)
                     at WinRT.ActivationFactory.Get(String typeName, Guid iid)
                     at Microsoft.UI.Xaml.Application.get__objRef_global__Microsoft_UI_Xaml_IApplicationStatics()
                     at Microsoft.UI.Xaml.Application.Start(ApplicationInitializationCallback callback)
                     at WinUIClassicSamplesBrowser.Program.Main(String[] args) in D:\gitSource\WinUI Classic Samples Browser\WinUIClassicSamplesBrowser\obj\x64\Debug\net8.0-windows10.0.22621.0\App.g.i.cs:line 26
                */

                if (e.Handled)
        {
            Debug.Fail(e.Message);
        }
    }

    /* TODO: $exception	{"Class not registered (0x80040154 (REGDB_E_CLASSNOTREG))"}	System.Runtime.InteropServices.COMException */
    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        GetService<IAppNotificationService>().Show(string.Format("AppNotificationSamplePayload".GetLocalized(), AppContext.BaseDirectory));

        await GetService<IActivationService>().ActivateAsync(args);
    }
}
