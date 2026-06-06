using Jnana.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Jnana.Vanara.Controls;

public sealed partial class SidebarControl : UserControl
{
    public enum SidebarDockMode
    {
        Default, // Use the default docking behavior (Stick to Edge. Outer: Navigation. Inner: Content)
        Left,
        Right
    }
    public enum FloatMode
    {
        Default, // Use the default floating behavior, Use threshold to determine when to float (e.g., when the window is too narrow)
        Always,
        Never,
    }
    public SidebarDockMode ChildDockMode { get; set; } = SidebarDockMode.Default;
    public FloatMode ChildFloatMode { get; set; } = FloatMode.Default;

    public SelectorItem[] Items = [new() { Label = "NuGets" }, new() { Label = "GitHub" }, new() { Label = "Samples" }, new() { Label = "Assemblies" }, new() { Label = "Utilities" }, new() { Label = "Settings" }];

    public SidebarControl()
    {
        // TODO: Implement logic to determine docking and floating behavior based on ChildDockMode and ChildFloatMode properties.
        // TODO: Ensure all Resources are properly defined and accessible.
        InitializeComponent();
        Loaded += SidebarControl_Loaded;
    }
    private void SidebarControl_Loaded(object sender, RoutedEventArgs e)
    {
//        foreach (var child in SidebarPanel.Children)
//        {
//            if (child is ToggleButton btn)
//                btn.Click += (s, e2) => NavigationService.TryNavigate(btn);
//        }
    }

    private void FeatureTile_OnClick(object? sender, EventArgs e)
    {
        Debug.Print("FeatureTile clicked");
    }

    private void VerticalSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // TODO: Implement logic to move the indicator to the selected item. This is a placeholder for the actual animation logic.
        if (VerticalSelector.ContainerFromItem(VerticalSelector.SelectedItem) is ListBoxItem item)
        {
//            var transform = new TranslateTransform();
//            IndicatorRoot.RenderTransform = transform;
//
//            var targetY = item.TransformToVisual(VerticalSelector).TransformPoint(new Point(0, 0)).Y;
//
//            var anim = new DoubleAnimation
//            {
//                To = targetY,
//                Duration = TimeSpan.FromMilliseconds(200),
//                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
//            };
//
//            transform.BeginAnimation(TranslateTransform.YProperty, anim);
        }
    }

}

public class SelectorItem : INotifyPropertyChanged
{
    public string Label { get; set; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
