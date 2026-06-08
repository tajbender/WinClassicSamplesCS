using CommunityToolkit.Mvvm.ComponentModel;
using Jnana.Views;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using static Jnana.Services.INavigationService;

namespace Jnana.Services;

public interface INavigationService
{
    public enum Area
    {
        Void,
        NuGets,
        GitHub,
        Samples,
        Disassembler,
        Utilities,
        Settings,
        BoulderDash,
    }

    void NavigateTo(Area area);
}

public partial class NavigationService : ObservableObject, INavigationService
{
    private readonly Frame _frame;
    private readonly Dictionary<Area, Type> _areaPageMap;

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
        Area.NuGets => new NuGetsPage(),
        Area.GitHub => new GitHubPage(),
        Area.Samples => new SamplesPage(),
        Area.Disassembler => new DisassemblerPage(),
        Area.Utilities => new UtilitiesPage(),
        Area.Settings => new SettingsPage(),
        Area.BoulderDash => new BoulderDashPage(),
        _ => new VoidPage()
    };

    public NavigationService(Frame frame)
    {
        _frame = frame ?? throw new ArgumentNullException(nameof(frame));

        _areaPageMap = new()
        {
            { Area.NuGets, typeof(NuGetsPage) },
            { Area.GitHub, typeof(GitHubPage) },
            { Area.Samples, typeof(SamplesPage) },
            { Area.Disassembler, typeof(DisassemblerPage) },
            { Area.Utilities, typeof(UtilitiesPage) },
            { Area.Settings, typeof(SettingsPage) },
            { Area.BoulderDash, typeof(BoulderDashPage) },
            { Area.Void, typeof(VoidPage) },
        };
    }

    public void NavigateTo(Area area)
    {
        if (_areaPageMap.TryGetValue(area, out var pageType))
        {
            Debug.Print($"Navigating to `{area}` page.");
            _frame.Navigate(pageType);
        }
        else
        {
            Debug.Print($"Failed to get page for `{area}` from PageMap.");
        }
    }

    //public void Navigate(Area area) { CurrentArea = area; }
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
}
