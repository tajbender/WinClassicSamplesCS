using Jnana.Views;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
//using System.Reflection.Metadata;
//using static ICSharpCode.Decompiler.SingleFileBundle;

namespace Jnana.Services;

public static class TabNavigationService
{
    private static TabView _tabView;
    public static IList<object> TabItems => _tabView.TabItems;

    public static void Initialize(TabView tabView)
    {
        _tabView = tabView;
    }
    public static void AddPageTab<T>(string header, Type pageType, object? parameter = null, bool selectTab = true) 
        where T : Control
    {
        var tab = new TabViewItem
        {
            Header = header,
            Content = Activator.CreateInstance(pageType, parameter)
        };

        _tabView.TabItems.Add(tab);
        if (selectTab)
            _tabView.SelectedItem = tab;
    }
    public static void AddGitHubPageTab(bool selectTab = true)
    {
        var tab = new TabViewItem
        {
            Header = "GitHub",
            Content = new GitHubPage()
        };

        _tabView.TabItems.Add(tab);
        if (selectTab)
            _tabView.SelectedItem = tab;
    }
    public static void AddNuGetsPageTab(bool selectTab = true)
    {
        var tab = new TabViewItem
        {
            Header = "NuGets",
            Content = new NuGetsPage()
        };

        _tabView.TabItems.Add(tab);
        if (selectTab)
            _tabView.SelectedItem = tab;
    }
    public static void AddSamplesPageTab(bool selectTab = true)
    {
        var tab = new TabViewItem
        {
            Header = "Samples",
            Content = new SamplesPage()
        };

        _tabView.TabItems.Add(tab);
        if (selectTab)
            _tabView.SelectedItem = tab;
    }
    public static void AddSettingsPageTab(bool selectTab = true)
    {
        var tab = new TabViewItem
        {
            Header = "Settings",
            Content = new SettingsPage()
        };

        _tabView.TabItems.Add(tab);
        if (selectTab)
            _tabView.SelectedItem = tab;
    }
}
