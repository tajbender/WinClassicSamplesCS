using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
//using LaunchActivatedEventArgs = Windows.ApplicationModel.Activation.LaunchActivatedEventArgs;

namespace Jnana;

public partial class App : Application
{
    private MainWindow? _mainWindow;

    public App()
    {
        InitializeComponent();
        // TODO: AppWindowTitleBar.SetIcon("Assets/VanaraMonkey.png");
        // TODO: AppWindowTitleBar.SetDragRegion(new Rect(0, 0, 100, 32));
    }
    private MainWindow? GetOrCreateMainWindow(bool allowInitialCreation = false)
    {
        if (_mainWindow == null && allowInitialCreation)
        {
            _mainWindow = new MainWindow
            {
//  ExtendsContentIntoTitleBar = true
            };

//  var titleBar = _mainWindow.AppWindow.TitleBar;
//  _mainWindow.SetTitleBar(MyDragRegion);
//  TODO: titleBar.SetIcon("Assets/VanaraMonkey.png");
        }
        // = //new Windows.Graphics.SizeInt32(1200, 800);
        //_Window.AppWindow.Size...



        return _mainWindow;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        GetOrCreateMainWindow(true)?.Activate();
    }
}
