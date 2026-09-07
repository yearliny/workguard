using System.Windows.Controls;
using System.Windows.Input;
using WorkGuard.Windows.Platform;

namespace WorkGuard.Windows.Views;

// Companion screens share one session; they never own timers or award completion.
internal sealed class RestCoverWindow : Window
{
    private readonly TextBlock _heading = new() { FontSize = 32, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _remaining = new() { FontSize = 18, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 20, 0, 20) };
    private bool _released;
    public RestCoverWindow(System.Windows.Forms.Screen screen, Action emergency)
    {
        Style = (Style)Application.Current.FindResource("AppWindow");
        Title = "工作防沉迷 · 休息中"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        Background = new SolidColorBrush(Color.FromRgb(233, 241, 245));
        var panel = new StackPanel { MaxWidth = 600, Margin = new Thickness(32), VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new Image { Source = (ImageSource)Application.Current.FindResource("SanctuaryArt"), Height = 280, Margin = new Thickness(0, 0, 0, 24) });
        Motion.SetEnter(panel, true);
        panel.Children.Add(_heading); panel.Children.Add(_remaining);
        var button = new Button { Content = "紧急退出", HorizontalAlignment = HorizontalAlignment.Center };
        button.Click += (_, _) => emergency(); panel.Children.Add(button); Content = panel;
        Loaded += (_, _) => WindowPlacement.PlaceOnScreen(this, screen);
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { emergency(); e.Handled = true; } };
        Closing += (_, e) => { if (!_released) { e.Cancel = true; emergency(); } };
    }
    public void Update(string heading, string remaining, Brush background)
    { _heading.Text = heading; _remaining.Text = remaining; Background = background; }
    public void Release() { _released = true; Close(); }
}
