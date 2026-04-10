using Microsoft.UI.Xaml;

namespace ClassicSamplesBrowser;

public static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.Start(p => new App());
    }
}
