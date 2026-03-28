using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using WinClassicSamplesBrowser.Contracts.Services;
using WinClassicSamplesBrowser.Services;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.ApplicationSettings;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinClassicSamplesBrowser;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    public IHost Host
    {
        get;
    }

    private Window? _window;

    public static WindowEx MainWindow { get; } = new MainWindow();

    public static UIElement? AppTitlebar { get; set; }

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
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
    services.AddTransient<WebViewViewModel>();
    services.AddTransient<WebViewPage>();
    services.AddTransient<MainViewModel>();
    services.AddTransient<MainPage>();
    services.AddTransient<ShellPage>();
    services.AddTransient<ShellViewModel>();

    // Configuration
    services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));
}).
Build();

        App.GetService<IAppNotificationService>().Initialize();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }

    public static T GetService<T>()
    where T : class
    {
        if ((App.Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
        {
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
        }

        return service;
    }
}
