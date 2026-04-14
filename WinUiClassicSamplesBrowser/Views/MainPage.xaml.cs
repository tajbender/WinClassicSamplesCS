using Microsoft.UI.Xaml.Controls;

namespace ClassicSamplesBrowser.Views;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();

        //ContentFrame.Navigate(typeof(HomePage));
    }

//    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
//    {
//        if (args.SelectedItem is NavigationViewItem item)
//        {
//            var tag = item.Tag?.ToString();
//
//            switch (tag)
//            {
//                case "HomePage":
//                    ContentFrame.Navigate(typeof(HomePage));
//                    //HeaderTitle.Text = "Home";
//                    break;
//
//                case "SamplesPage":
//                    ContentFrame.Navigate(typeof(SamplesPage));
//                    //HeaderTitle.Text = "Samples";
//                    break;
//
//                case "AboutPage":
//                    ContentFrame.Navigate(typeof(AboutPage));
//                    //HeaderTitle.Text = "About";
//                    break;
//            }
//        }
//    }
}