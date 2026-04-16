using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ClassicSamplesBrowser.Vanara.Controls;

public sealed partial class FeatureTile : UserControl
{
    public FeatureTile()
    {
        InitializeComponent();
        SetupInteractions();
    }

    // Dependency Properties
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(FeatureTile),
            new PropertyMetadata(string.Empty, (d, e) =>
            {
                ((FeatureTile)d).TitleElement.Text = (string)e.NewValue;
            }));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(FeatureTile),
            new PropertyMetadata(string.Empty, (d, e) =>
            {
                ((FeatureTile)d).SubtitleElement.Text = (string)e.NewValue;
            }));

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(FeatureTile),
            new PropertyMetadata(string.Empty, (d, e) =>
            {
                ((FeatureTile)d).IconElement.Glyph = (string)e.NewValue;
            }));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    private void SetupInteractions()
    {
        Root.PointerEntered += (_, __) =>
        {
            ScaleTransform.ScaleX = 1.03;
            ScaleTransform.ScaleY = 1.03;
            Root.Opacity = 0.95;
        };

        Root.PointerExited += (_, __) =>
        {
            ScaleTransform.ScaleX = 1.0;
            ScaleTransform.ScaleY = 1.0;
            Root.Opacity = 1.0;
        };

        Root.PointerPressed += (_, __) =>
        {
            ScaleTransform.ScaleX = 0.97;
            ScaleTransform.ScaleY = 0.97;
        };

        Root.PointerReleased += (_, __) =>
        {
            ScaleTransform.ScaleX = 1.03;
            ScaleTransform.ScaleY = 1.03;
            Click?.Invoke(this, EventArgs.Empty);
        };
    }

    public event EventHandler Click;
}
