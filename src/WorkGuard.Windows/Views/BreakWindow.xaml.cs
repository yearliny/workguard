using WorkGuard.Windows.Platform;
using System.Windows.Input;

namespace WorkGuard.Windows.Views;

public partial class BreakWindow : Window
{
    private readonly BreakSession _session;
    private readonly bool _sound;
    private int _lastIndex;
    private bool _started;
    private bool _finishing;
    public bool HasStarted => _started;
    public bool IsRunning => _started && !_session.Paused && !_session.Finished;
    public event Action<BreakSession>? Ended;
    public event Action<BreakSession>? Completed;

    public BreakWindow(BreakKind kind, bool gentle, bool automatic, TimeSpan continuous, bool sound = true)
    {
        InitializeComponent();
        _session = new BreakSession(kind, gentle);
        _sound = sound;
        ShowActivated = !automatic;
        if (kind == BreakKind.Eyes)
        {
            Width = 490; Height = 620;
            Heading.Text = "目光，放远一点";
            Instruction.Text = "准备好后，看向窗外或远处 20 秒。结束时可播放提示音，不必盯着屏幕。";
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
        Loaded += (_, _) => WindowPlacement.Place(this, false, kind == BreakKind.Eyes);
        Closed += (_, _) => Ended?.Invoke(_session);
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) PauseForInterruption(); };
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        _started = true;
        if (_session.Kind != BreakKind.Eyes) WindowPlacement.Place(this, true, false);
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
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        else if (e.Key == Key.Space && e.OriginalSource is not System.Windows.Controls.Button)
        { if (!_started) Start_Click(sender, e); else if (!_session.Finished) Pause_Click(sender, e); e.Handled = true; }
    }

    private void Render()
    {
        if (_session.Finished)
        {
            if (_finishing) return;
            _finishing = true;
            if (_session.FullyCompleted) Completed?.Invoke(_session);
            if (_session.Kind == BreakKind.Eyes)
            {
                if (_session.FullyCompleted && _sound) System.Media.SystemSounds.Asterisk.Play();
                Close();
                return;
            }
            NextStep.Text = "";
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
        if (_lastIndex != _session.Index && _sound) System.Media.SystemSounds.Asterisk.Play();
        _lastIndex = _session.Index;
        NextStep.Text = _session.Index + 1 < _session.Exercises.Count ? "接下来 · " + _session.Exercises[_session.Index + 1].Title : "这是最后一个动作";
        Countdown.Text = TimeSpan.FromSeconds(Math.Ceiling(_session.Remaining.TotalSeconds)).ToString(@"mm\:ss");
        Progress.Value = 100 * _session.StepElapsed.TotalSeconds / exercise.Seconds;
        PauseButton.Content = _session.Paused ? "继续" : "暂停";
        StepLabel.Text = $"{_session.Index + 1} / {_session.Exercises.Count} · 已活动 {(int)_session.Observed.TotalSeconds} 秒";
    }

}
