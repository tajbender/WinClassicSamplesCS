using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace ClassicSamplesBrowser.Vanara.Controls;

public sealed partial class FloatingStatusBar : UserControl
{
    private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromSeconds(3) };

    // TODO: Add entrance/exit animations, and maybe a progress ring for long-running operations.
    // TODO: Let the `Ready.` blink like an 8 bit machine.

    public FloatingStatusBar()
    {
        InitializeComponent();
        _hideTimer.Tick += (_, __) => Hide();
    }

    public void Show(string message, string icon = "\uE946")
    {
        MessageElement.Text = message;
        IconElement.Glyph = icon;

        Root.Opacity = 1;
        Root.Translation = new System.Numerics.Vector3(0, 0, 0);

        _hideTimer.Stop();
        _hideTimer.Start();
    }

    public void Hide()
    {
        Root.Opacity = 0;
        Root.Translation = new System.Numerics.Vector3(0, 20, 0);
    }
}
