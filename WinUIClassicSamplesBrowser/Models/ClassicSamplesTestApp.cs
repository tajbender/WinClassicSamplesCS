using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using WinUIClassicSamplesBrowser.Contracts.Services;
using WinUIClassicSamplesBrowser.Services;

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
