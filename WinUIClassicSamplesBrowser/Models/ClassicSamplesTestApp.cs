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
    private string name;

    [ObservableProperty]
    private string relativePath;

    [ObservableProperty]
    private bool isAvailable;

    [ObservableProperty]
    private bool isRunning;

    public ClassicSamplesTestApp(string relativePath)
    {
        this.relativePath = relativePath;
        this.name = Path.GetFileName(relativePath);
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
