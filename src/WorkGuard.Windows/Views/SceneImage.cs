using System.Windows.Controls;

namespace WorkGuard.Windows.Views;

// Rounded native image surface. ImageBrush crops inside the border at every aspect ratio.
public sealed class SceneImage : Border
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(ImageSource), typeof(SceneImage), new PropertyMetadata(null, Changed));
    public ImageSource? Source { get => (ImageSource?)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    public SceneImage() { IsHitTestVisible = false; }
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    { ((SceneImage)d).Background = e.NewValue is ImageSource image ? new ImageBrush(image) { Stretch = Stretch.UniformToFill } : null; }
}
