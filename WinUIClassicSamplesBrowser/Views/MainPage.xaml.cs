using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using WinUIClassicSamplesBrowser.Models;
using WinUIClassicSamplesBrowser.ViewModels;

namespace WinUIClassicSamplesBrowser.Views;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel
    {
        get;
    } = App.GetService<MainViewModel>();

    public ClassicSamplesTestApp[] SampleApps => ViewModel.SampleApps;

//    [ObservableProperty]
//    private ClassicSamplesTestApp[] samples
//    {
//        get => ViewModel.SampleApps;
//        set => ViewModel.SampleApps = value;
//    }

    public MainPage()
    {
        InitializeComponent();
    }
}
