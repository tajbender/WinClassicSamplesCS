using CommunityToolkit.Mvvm.ComponentModel;
using WinUIClassicSamplesBrowser.Contracts.Services;
using WinUIClassicSamplesBrowser.Models;

namespace WinUIClassicSamplesBrowser.ViewModels;

public partial class MainViewModel : ObservableRecipient
{
    public ClassicSamplesTestApp[] SampleApps;
    public IFileService FileService;

    public MainViewModel()
    {
        SampleApps = new ClassicSamplesTestApp[] {
            new("*"),
        };
    }
}
