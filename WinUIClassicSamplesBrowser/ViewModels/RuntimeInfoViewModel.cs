using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyModel;
using Windows.ApplicationModel.DataTransfer;

namespace WinUIClassicSamplesBrowser.ViewModels;

public partial class RuntimeInfoViewModel : ObservableObject
{
    public ObservableCollection<AssemblyInfoModel> LoadedAssemblies
    {
        get;
    }
        = new();

    public ObservableCollection<LibraryInfoModel> ReferencedLibraries
    {
        get;
    }
        = new();

    public ObservableCollection<KeyValueItem> TableItems
    {
        get;
    }
        = new();

    public RuntimeInfoViewModel()
    {
        LoadLoadedAssemblies();
        LoadReferencedLibraries();
    }

    [RelayCommand]
    private void CopyTableToClipboard()
    {
        var sb = new StringBuilder();

        foreach (var item in TableItems)
            sb.AppendLine($"{item.Key}: {item.Value}");

        Clipboard.SetContent(
            new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy,
                // taj                Text = sb.ToString()
            });
    }

    [RelayCommand]
    private void ExportMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("| Key | Value |");
        sb.AppendLine("|-----|--------|");

        foreach (var item in TableItems)
            sb.AppendLine($"| {item.Key} | {item.Value} |");

        Clipboard.SetContent(
            new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy,
                // taj                Text = sb.ToString()
            });
    }

    private void LoadLoadedAssemblies()
    {
        var assemblies = AppDomain.CurrentDomain
            .GetAssemblies()
            .OrderBy(a => a.GetName().Name);

        foreach (var asm in assemblies)
        {
            var name = asm.GetName();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            LoadedAssemblies.Add(new AssemblyInfoModel
            {
                Name = name.Name,
                Version = name.Version?.ToString(),
                InformationalVersion = info,
                Location = SafeGetLocation(asm)
            });
        }
    }

    private void LoadReferencedLibraries()
    {
        var context = DependencyContext.Default;

        if (context == null)
            return;

        foreach (var lib in context.RuntimeLibraries.OrderBy(l => l.Name))
        {
            ReferencedLibraries.Add(new LibraryInfoModel
            {
                Name = lib.Name,
                Version = lib.Version,
                Type = lib.Type,
                Path = string.Join(";", lib.RuntimeAssemblyGroups
                    .SelectMany(g => g.AssetPaths))
            });
        }
    }

    private static string SafeGetLocation(Assembly asm)
    {
        try
        {
            return asm.Location;
        }
        catch
        {
            return "(dynamic / in-memory)";
        }
    }

    public void LoadTableFromAssembly(AssemblyInfoModel asm)
    {
        TableItems.Clear();

        TableItems.Add(new KeyValueItem { Key = "Name", Value = asm.Name });
        TableItems.Add(new KeyValueItem { Key = "Version", Value = asm.Version });
        TableItems.Add(new KeyValueItem { Key = "Informational Version", Value = asm.InformationalVersion });
        TableItems.Add(new KeyValueItem { Key = "Location", Value = asm.Location });
    }
}

public class AssemblyInfoModel
{
    public string Name
    {
        get; set;
    }
    public string Version
    {
        get; set;
    }
    public string InformationalVersion
    {
        get; set;
    }
    public string Location
    {
        get; set;
    }
}

public class LibraryInfoModel
{
    public string Name
    {
        get; set;
    }
    public string Version
    {
        get; set;
    }
    public string Type
    {
        get; set;
    }
    public string Path
    {
        get; set;
    }
}

