using CommunityToolkit.Mvvm.ComponentModel;
using WinUIClassicSamplesBrowser.Contracts.Services;
using WinUIClassicSamplesBrowser.Models;

namespace WinUIClassicSamplesBrowser.ViewModels;

public partial class MainViewModel : ObservableRecipient
{
    private static readonly ClassicSamplesTestApp[] DefaultSampleApps = [
        new(@"*.\TestExampleA\bin\UnitTest.exe"),
        new(@"*.\TestExample2\bin\UnitTest.exe"),
        new(@"*.\TestExample3\bin\UnitTest.exe")
    ];

    [ObservableProperty]
    private ClassicSamplesTestApp[] _sampleApps = DefaultSampleApps;
}
