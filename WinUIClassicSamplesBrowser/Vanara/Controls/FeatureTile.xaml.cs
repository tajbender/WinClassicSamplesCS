using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ClassicSamplesBrowser.Vanara.Controls;

public sealed partial class FeatureTile : UserControl
{
    public FeatureTile()
    {
        InitializeComponent();
        SetupHoverAnimation();
    }

    public string Title
    {
        get => TitleElement.Text;
        set => TitleElement.Text = value;
    }

    public string Subtitle
    {
        get => SubtitleElement.Text;
        set => SubtitleElement.Text = value;
    }

    public string Icon
    {
        get => IconElement.Glyph;
        set => IconElement.Glyph = value;
    }

    private void SetupHoverAnimation()
    {
        Root.PointerEntered += (_, __) =>
        {
            Root.Translation = new System.Numerics.Vector3(0, -2, 0);
            Root.Opacity = 0.95;
        };

        Root.PointerExited += (_, __) =>
        {
            Root.Translation = new System.Numerics.Vector3(0, 0, 0);
            Root.Opacity = 1.0;
        };
    }
}
