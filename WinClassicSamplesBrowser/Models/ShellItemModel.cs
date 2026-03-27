using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinClassicSamplesBrowser.Models;

public interface IShellItemModel
{
    string Name { get; }
    string? DisplayName { get; }
    string? ParsingName { get; }
    string? TypeName { get; }
    bool IsFolder { get; }
    Task<IconSource?> GetIconAsync();
}

public interface IShellFolderModel : IShellItemModel
{
    Task<IReadOnlyList<IShellItemModel>> GetChildrenAsync();
}

public class ShellItemModel : ObservableObject, IShellItemModel
{
    public string Name { get; }
    public string? DisplayName { get; }
    public string? ParsingName { get; }
    public string? TypeName { get; }
    public bool IsFolder { get; }

    private readonly Func<Task<IconSource?>> _iconLoader;
    private IconSource? _icon;

    public ShellItemModel(
        string name,
        string? displayName,
        string? parsingName,
        string? typeName,
        bool isFolder,
        Func<Task<IconSource?>> iconLoader)
    {
        Name = name;
        DisplayName = displayName;
        ParsingName = parsingName;
        TypeName = typeName;
        IsFolder = isFolder;
        _iconLoader = iconLoader;
    }

    public async Task<IconSource?> GetIconAsync()
    {
        if (_icon != null)
            return _icon;

        _icon = await _iconLoader();
        return _icon;
    }
}
