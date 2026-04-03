using Microsoft.UI.Xaml.Controls;

using WinUIClassicSamplesBrowser.ViewModels;

namespace WinUIClassicSamplesBrowser.Views;

// To learn more about WebView2, see https://docs.microsoft.com/microsoft-edge/webview2/.
public sealed partial class WebBrowserPage : Page
{
    public WebBrowserViewModel ViewModel
    {
        get;
    } = App.GetService<WebBrowserViewModel>();

    public WebBrowserPage()
    {
        InitializeComponent();

        ViewModel.WebViewService.Initialize(WebView);
    }
}
