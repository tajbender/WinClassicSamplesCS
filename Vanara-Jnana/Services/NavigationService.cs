using CommunityToolkit.Mvvm.ComponentModel;
using Jnana.Views;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using System.Diagnostics;
using Vanara.PInvoke;
using static Jnana.Services.INavigationService;

namespace Jnana.Services;

public interface INavigationService
{
    public enum Area
    {
        Void,
        Settings
    }

    void NavigateTo(Area area);
}

public partial class NavigationService : ObservableObject, INavigationService
{
    public Area CurrentArea
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(CurrentPage));
            }
        }
    } = Area.Void;

    public Page CurrentPage => CurrentArea switch
    {
        Area.Settings => new SettingsPage(),
        _ => new VoidPage()
    };

    private readonly Frame _frame;
    private readonly Dictionary<Area, Type> _registry;

    public NavigationService(Frame frame)
    {
        _frame = frame;

        _registry = new()
        {
            { Area.Void, typeof(VoidPage) },
            { Area.Settings, typeof(SettingsPage) }
        };

        // INFO: @dahall - select a default page to navigate to on loading, to ensure the main content area is populated with a page, and to establish a consistent starting point for navigation
        //NavigateTo(Area.Void);
        NavigateTo(Area.Settings);
    }

    //    // TODO:
    //    //    public static void Navigate(Shell32.IShellFolder shellFolder)
    //    //    {
    //    //        TryNavigate(shellFolder);
    //    //    }
    //    public bool TryNavigate(object target, bool allowPageCreation = true)
    //    {
    //        try
    //        {
    //            var pageType = target switch
    //            {
    //                "Assemblies" => typeof(AssembliesPage),
    //                "GitHub" => typeof(GitHubPage),
    //                "NuGets" => typeof(NuGetsPage),
    //                "Samples" => typeof(SamplesPage),
    //                "Settings" => typeof(SettingsPage),
    //                "Start" => typeof(StartPage),
    //                "Utilities" => typeof(UtilitiesPage),
    //                _ => null
    //            };
    //
    //            // Check if the page type is null and if page creation is allowed
    //            if (pageType == null && !allowPageCreation)
    //            {
    //                //LogWriter.PrintLine("Page type is null and page creation is not allowed.");
    //                return false;
    //            }
    //
    //            // TODO: _frame.Navigate(pageType ?? new UtilitiesPage());
    //            //return _frame.Navigate(pageType);
    //            return true;
    //        }
    //        catch (Exception ex)
    //        {
    //            Debug.Fail(ex.ToString());
    //            throw;
    //        }
    //    }

    public static void NavigateBack() { }

    public static void Forward() { }

    public void NavigateTo(Area area)
    {
        if (_registry.TryGetValue(area, out var pageType))
        {
            _frame.Navigate(pageType);
        }
    }

    public void Navigate(Area area)
    {
        CurrentArea = area;
    }
}
