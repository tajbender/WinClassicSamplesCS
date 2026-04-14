using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ClassicSamplesBrowser.Views;

public sealed partial class StartPage : Page
{
    public StartPage()
    {
        InitializeComponent();

        //ContentFrame.Navigate(typeof(HomePage));
    }

    private void LoadAssemblies_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("LoadAssemblies_Click event handler called.");
    }

    private void OpenExplorer_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("OpenExplorer_Click event handler called.");
    }

    private void OpenSamples_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("OpenSamples_Click event handler called.");
    }
}