using Jnana.Models.Contracts;
using Jnana.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.Diagnostics;

namespace Jnana.Views;

/// <summary><completionlist cref="StartPage"></completionlist>
/// StartPage is the main page that is shown when the app is launched
/// and serves as a navigation hub for the various Views in the app.
/// </summary>
public sealed partial class StartPage : Page,
    INavigationAware
{
    public StartPage()
    {
        InitializeComponent();
        DataContext = this;
        Loading += StartPage_Loading;
        //TabNavigationService.Initialize(MainTabs);

        //global::System.Uri resourceLocator = new global::System.Uri("ms-appx:///Views/StartPage.xaml");
        //var resourceInfo = Windows.ApplicationModel.Package.Current.InstalledLocation.GetFileAsync("Views/StartPage.xaml").AsTask().Result;
        //Debug.WriteLine($"Resource locator: {resourceLocator}");
    }
    private void MainTabs_AddTabButtonClick(TabView sender, object args)
    {
        Debug.WriteLine($"MainTabs_AddTabButtonClick({sender}, {args})");
        try
        {
            // var tab = new TabViewItem { Header = "NuGet", Content = new NuGetsPage { DataContext = NuGetVM } };
            // sender.TabItems.Add(tab);
            // sender.SelectedItem = tab;
            TabNavigationService.AddGitHubPageTab();  // TODO: .NavigateTo()
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MainTabs_AddTabButtonClick() {ex.Message}");
        }
    }
    private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        Debug.WriteLine($"MainTabs_SelectionChanged({sender}, {args})");

        //if (MainTabs.SelectedItem is TabViewItem tab &&
        //    tab.Content is Frame frame)
        //{
        //    Debug.WriteLine($".Content: {frame}");
        //    // TODO: TabViewContentPresenter.Content = frame.Content;
        //}
    }
    private void StartPage_Loading(FrameworkElement sender, object args)
    {
        try
        {
            //TabNavigationService.AddNuGetsPageTab(selectTab: false);
            //TabNavigationService.AddGitHubPageTab(selectTab: false);
            //TabNavigationService.AddPageTab<SamplesPage>("Samples", typeof(SamplesPage), parameter: SamplesVM, selectTab: false);
            //TabNavigationService.AddSamplesPageTab(selectTab: false);
            //TabNavigationService.AddSettingsPageTab(selectTab: false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"StartPage_Loading() {ex.Message}");
        }
    }
}
