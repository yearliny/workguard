using System.Windows.Input;
using System.Windows.Threading;

namespace WorkGuard.Windows.Views;

public partial class ReminderWindow : Window
{
    private readonly DispatcherTimer _dismiss = new() { Interval = TimeSpan.FromSeconds(40) };
    public bool Accepted { get; private set; }
    public BreakKind Kind { get; }
    public ReminderWindow(BreakKind kind)
    {
        InitializeComponent(); Kind = kind;
        Heading.Text = kind == BreakKind.Eyes ? "让目光，走远一点" : "给身体，留一个空隙";
        Description.Text = kind == BreakKind.Eyes ? "用 20 秒看看远处，放松一下眼睛。" : "起来走一走，用 3 分钟换个姿势。手头的事可以稍后接着做。";
        StartButton.Content = kind == BreakKind.Eyes ? "远眺 20 秒" : "活动 3 分钟";
        if (kind == BreakKind.Office)
        {
            Heading.Text = "赴一个自己的约";
            Description.Text = "你安排的 7 分钟到了。站起来，给身体一点活动的时间。";
            StartButton.Content = "开始 7 分钟";
            DismissButton.Content = "今天略过";
            Footnote.Text = "每天邀请一次 · 40 秒后收起 · 不会自动全屏";
        }
        Loaded += (_, _) => { Platform.WindowPlacement.Place(this, fullscreen: false, corner: true); _dismiss.Start(); };
        _dismiss.Tick += (_, _) => Close();
        Closed += (_, _) => _dismiss.Stop();
    }
    private void Start_Click(object sender, RoutedEventArgs e) { Accepted = true; Close(); }
    private void Dismiss_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) Close(); }
}
