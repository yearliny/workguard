using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace WorkGuard.Windows.Views;

public partial class BreakWindow : Window
{
    private readonly BreakSession _session;
    private readonly Forms.Screen _screen;
    private bool _started;
    private bool _finishing;
    public bool HasStarted => _started;
    public bool IsRunning => _started && !_session.Paused && !_session.Finished;
    public event Action<BreakSession>? Ended;
    public event Action<BreakSession>? Completed;

    public BreakWindow(BreakKind kind, bool gentle, bool automatic, TimeSpan continuous)
    {
        InitializeComponent();
        _session = new BreakSession(kind, gentle);
        _screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        ShowActivated = !automatic;
        if (kind == BreakKind.Eyes)
        {
            Width = 490; Height = 620;
            Heading.Text = "目光，放远一点";
            Instruction.Text = "准备好后，看向窗外或远处 20 秒。结束时会播放系统提示音，不必盯着屏幕。";
            StartButton.Content = "开始 20 秒远眺";
            Footnote.Text = "这只是休息提示，不需要用力眨眼或揉眼。";
        }
        else
        {
            Heading.Text = kind == BreakKind.Office ? "留 7 分钟给身体" : "起来，走动一下";
            Instruction.Text = "暂时离开座位，跟随简单的活动提示。\n动作以舒适为准，可以随时退出。";
            StartButton.Content = kind == BreakKind.Office ? "开始 7 分钟活动" : "开始 3 分钟活动";
        }
        Eyebrow.Text = $"已连续工作约 {(int)continuous.TotalMinutes} 分钟";
        Countdown.Text = Programs.Duration(kind).ToString(@"mm\:ss");
        Progress.Visibility = Visibility.Collapsed;
        SourceInitialized += (_, _) => Place(kind);
        Closed += (_, _) => Ended?.Invoke(_session);
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) PauseForInterruption(); };
    }

    private void Place(BreakKind kind)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var bounds = kind == BreakKind.Eyes ? _screen.WorkingArea : _screen.Bounds;
        var source = HwndSource.FromHwnd(handle);
        var scale = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var width = kind == BreakKind.Eyes ? Math.Min((int)(Width * scale.M11), bounds.Width) : bounds.Width;
        var height = kind == BreakKind.Eyes ? Math.Min((int)(Height * scale.M22), bounds.Height) : bounds.Height;
        var left = kind == BreakKind.Eyes ? bounds.Right - width : bounds.Left;
        var top = kind == BreakKind.Eyes ? bounds.Bottom - height : bounds.Top;
        SetWindowPos(handle, new IntPtr(-1), left, top, width, height, 0x0010); // SWP_NOACTIVATE
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        _started = true;
        StartButton.Visibility = Visibility.Collapsed;
        SnoozeButton.Visibility = Visibility.Collapsed;
        PauseButton.Visibility = Visibility.Visible;
        SkipButton.Visibility = _session.Kind == BreakKind.Eyes ? Visibility.Collapsed : Visibility.Visible;
        Progress.Visibility = Visibility.Visible;
        Render();
        PauseButton.Focus();
    }

    public void Tick(TimeSpan elapsed) { _session.Advance(elapsed); Render(); }
    public void PauseForInterruption()
    {
        if (!_started || _session.Finished) return;
        _session.Paused = true;
        Render();
    }
    private void Pause_Click(object sender, RoutedEventArgs e) { _session.Paused = !_session.Paused; Render(); }
    private void Skip_Click(object sender, RoutedEventArgs e) { _session.Skip(); Render(); }
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) Close(); }

    private void Render()
    {
        if (_session.Finished)
        {
            if (_finishing) return;
            _finishing = true;
            if (_session.FullyCompleted) Completed?.Invoke(_session);
            if (_session.Kind == BreakKind.Eyes)
            {
                if (_session.FullyCompleted) System.Media.SystemSounds.Asterisk.Play();
                Close();
                return;
            }
            Heading.Text = _session.FullyCompleted ? "好了，带着轻松回来" : "这次就到这里";
            Instruction.Text = _session.FullyCompleted ? "活动流程已完成。准备好了，再继续工作。" : "你跳过了部分动作，这次不会记为完整活动。";
            Countdown.Text = _session.Observed.ToString(@"mm\:ss");
            PauseButton.Visibility = SkipButton.Visibility = Visibility.Collapsed;
            SnoozeButton.Content = "回到工作";
            SnoozeButton.Visibility = Visibility.Visible;
            Progress.Value = 100;
            StepLabel.Text = "不必追求打卡，舒服地活动就好。";
            return;
        }
        var exercise = _session.Current;
        Eyebrow.Text = _session.Paused ? "已暂停 · 准备好后继续" : "给身体一点空间";
        Heading.Text = exercise.Title;
        Instruction.Text = exercise.Instruction;
        Glyph.Text = exercise.Glyph;
        Countdown.Text = TimeSpan.FromSeconds(Math.Ceiling(_session.Remaining.TotalSeconds)).ToString(@"mm\:ss");
        Progress.Value = 100 * _session.StepElapsed.TotalSeconds / exercise.Seconds;
        PauseButton.Content = _session.Paused ? "继续" : "暂停";
        StepLabel.Text = $"{_session.Index + 1} / {_session.Exercises.Count} · 已活动 {(int)_session.Observed.TotalSeconds} 秒";
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
}
