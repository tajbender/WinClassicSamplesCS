using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Jnana.Vanara.Controls;

public sealed partial class FloatingStatusBar : UserControl,
    INotifyPropertyChanged
{
    private bool _autoHide = true;

    /// <summary>The timer used to hide the status bar automatically.</summary>
    private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromSeconds(2) };

    public FloatingStatusBar()
    {
        InitializeComponent();
        _hideTimer.Tick += (s, e) => Hide();
        Show("READY.");

        /*  TODO: Sizer Glyphs:
            Klassischer Resize‑Grip	E7BF	„GripperResize“ – diagonale Linien, wirkt wie der alte Win32‑Grip
            Alternative minimal	    E7C0	„GripperBarHorizontal“ – drei Punkte, subtiler Look
            Symbolisch	            E7C1	„GripperBarVertical“ – vertikale Punkte, wenn du rechts unten eine Spalte andeutest
         */
    }
    public void Show(string message, string icon = "\uE946")
    {
        MessageElement.Text = message;
        IconElement.Glyph = icon;

        ControlRoot.Opacity = 1;
        ControlRoot.Translation = new System.Numerics.Vector3(0, 0, 0);

        _hideTimer.Stop();
        _hideTimer.Start();
    }
    public void Hide()
    {
        ControlRoot.Opacity = 0;
        ControlRoot.Translation = new System.Numerics.Vector3(0, 20, 0);
    }
    // INotifyPropertyChanged implementation
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
