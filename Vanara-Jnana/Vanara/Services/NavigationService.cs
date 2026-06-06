using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jnana.Views;

namespace Jnana.Vanara.Services;

public static class NavigationService
{
    // The Frame control used for navigation
    private static Frame _frame;

    public static void Initialize(Frame frame)
    {
        _frame = frame;
    }

    public static List<Frame> NavigationHistory { get; } = [];

    // TODO: Events: Navigated, Navigating, NavigationFailed, NavigationStopped

    public static bool CanGoBack => _frame.CanGoBack;
    public static bool CanGoForward => _frame.CanGoForward;


    public static void NavigateToStart()
        => Navigate(typeof(StartPage));

    public static void NavigateToSamples()
        => Navigate(typeof(SamplesPage));

    public static void Navigate(Type pageType, object parameter = null)
    {
        try
        {
            _frame.Navigate(pageType, parameter);
        }
        catch (Exception ex)
        {
            Debug.Fail(ex.ToString());
            throw;
        }
    }

    public static void GoBack()
    {
        if(CanGoBack)
        {
            _frame.GoBack();
        }
    }

    public static void GoForward()
    {
        if(CanGoForward)
        {
            _frame.GoForward();
        }
    }

    //// object = null; => Home!
    //public bool Navigate(object navigationTarget, object? parameter = null, bool writeHistory = true)
    //{
    //    try 
    //    {
    //        if (navigationTarget != null)
    //        {
    //            return parameter != null ?
    //                Frame.Navigate(navigationTarget.GetType(), parameter) 
    //                : Frame.Navigate(navigationTarget.GetType());
    //        }

    //        // TODO: search the web
    //    }
    //    catch (Exception ex)
    //    {
    //        // Handle navigation exceptions as needed.
    //        Debug.WriteLine($"Navigation error: {ex.Message}");
    //        return false;
    //    }

    //    if (writeHistory)
    //    {
    //        // Navigate to the target page and add it to the navigation history
    //    }

    //    return true;
    //}
}
