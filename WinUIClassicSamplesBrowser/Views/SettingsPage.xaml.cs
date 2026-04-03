using Microsoft.UI.Xaml.Controls;

using WinUIClassicSamplesBrowser.ViewModels;

namespace WinUIClassicSamplesBrowser.Views;

// TODO: Set the URL for your privacy policy by updating SettingsPage_PrivacyTermsLink.NavigateUri in Resources.resw.
public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel
    {
        get;
    } = App.GetService<SettingsViewModel>();

    public SettingsPage()
    {
        InitializeComponent();
    }

    private void OnAssemblySelected(AssemblyInfoModel asm)
    {
// TODO:        ViewModel.LoadTableFromAssembly(asm);
    }
}
