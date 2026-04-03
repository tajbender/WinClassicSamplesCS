using CommunityToolkit.Mvvm.ComponentModel;
using WinUIClassicSamplesBrowser.Contracts.Services;
using WinUIClassicSamplesBrowser.Models;

namespace WinUIClassicSamplesBrowser.ViewModels;

public partial class MainViewModel : ObservableRecipient
{
    [ObservableProperty]
    private ClassicSamplesTestApp[] _sampleApps = new ClassicSamplesTestApp[] {
        new(@"*.\TestExampleA\bin\UnitTest.exe"),
        new(@"*.\TestExample2\bin\UnitTest.exe"),
        new(@"*.\TestExample3\bin\UnitTest.exe"),
    };

    public IFileService FileService;
}
