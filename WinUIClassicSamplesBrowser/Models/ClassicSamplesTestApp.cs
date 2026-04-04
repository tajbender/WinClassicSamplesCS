using CommunityToolkit.Mvvm.ComponentModel;
using WinUIClassicSamplesBrowser.Contracts.Services;

namespace WinUIClassicSamplesBrowser.Models;

public partial class ClassicSamplesTestApp : ObservableObject
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _relativePath;

    [ObservableProperty]
    private bool _isAvailable;

    [ObservableProperty]
    private bool _isRunning;

//    private ImagePath _imageSource;

    public ClassicSamplesTestApp(string relativePath)
    {
        _relativePath = relativePath;
        _name = Path.GetFileName(relativePath);
    }

    public async Task CheckAvailability(IFileService fileService)
    {
        // TODO: Test for file exists

        if (fileService.IsExecutable(RelativePath))
        {
            IsAvailable = true;
        }
    }
}
