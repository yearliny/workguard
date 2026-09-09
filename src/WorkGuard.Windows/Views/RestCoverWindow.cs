using System.Windows.Controls;
using System.Windows.Input;
using WorkGuard.Windows.Platform;

namespace WorkGuard.Windows.Views;

// Companion screens share one session; they never own timers or award completion.
internal sealed class RestCoverWindow : Window
{
    private readonly TextBlock _heading = new() { FontSize = 32, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _remaining = new() { FontSize = 18, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 20, 0, 20) };
    private readonly Image _art = new() { Height = 340, Margin = new Thickness(0, 0, 0, 24), Source = RestArtwork.For(RestScene.Distance) };
    private readonly ProgressBar _progress = new() { Height = 8, Minimum = 0, Maximum = 100, Margin = new Thickness(0, 0, 0, 24) };
    private bool _released;
    public RestCoverWindow(System.Windows.Forms.Screen screen, Action emergency)
    {
        Style = (Style)Application.Current.FindResource("AppWindow");
        Title = "工作防沉迷 · 休息中"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        Background = new SolidColorBrush(Color.FromRgb(233, 241, 245));
        var panel = new StackPanel { MaxWidth = 600, Margin = new Thickness(32), VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(_art);
        Motion.SetEnter(panel, true);
        panel.Children.Add(_heading); panel.Children.Add(_remaining); panel.Children.Add(_progress);
        var button = new Button { Content = "紧急退出", Style = (Style)Application.Current.FindResource("RestTextButton"), HorizontalAlignment = HorizontalAlignment.Center };
        button.Click += (_, _) => emergency(); panel.Children.Add(button); Content = panel;
        Loaded += (_, _) => WindowPlacement.PlaceOnScreen(this, screen);
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { emergency(); e.Handled = true; } };
        Closing += (_, e) => { if (!_released) { e.Cancel = true; emergency(); } };
    }
    public void Update(string heading, string remaining, Brush background, ImageSource? art = null, double progress = 0)
    { _heading.Text = heading; _remaining.Text = remaining; Background = background; if (art is not null) _art.Source = art; _progress.Value = progress; }
    public void Release() { _released = true; Close(); }
}
