using WorkGuard.Windows.Platform;
using System.Windows.Input;

namespace WorkGuard.Windows.Views;

public partial class BreakWindow : Window
{
    private readonly BreakSession _session;
    private readonly bool _sound;
    private readonly bool _strict;
    private readonly List<RestCoverWindow> _covers = [];
    private bool _allowClose;
    private RestScene? _lastScene;
    public bool IsStrict => _strict;
    private int _lastIndex;
    private int _routeIndex = -1;
    private bool _started;
    private bool _finishing;
    public bool HasStarted => _started;
    public bool IsFinished => _session.Finished;
    public bool IsRunning => _started && !_session.Paused && !_session.Finished;
    public event Action<BreakSession>? Ended;
    public event Action<BreakSession>? Completed;

    public BreakWindow(BreakKind kind, bool gentle, bool automatic, TimeSpan continuous, bool sound = true, bool strict = false, int variant = 0, bool neck = false)
    {
        InitializeComponent();
        _session = new BreakSession(kind, gentle, strict, variant, neck);
        _strict = strict;
        _sound = sound;
        ShowActivated = strict || !automatic;
        if (kind == BreakKind.Eyes)
        {
            Width = 720; Height = 700;
            RestArt.Source = RestArtwork.For(RestScene.Distance);
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
        if (strict)
        {
            ExitButton.Content = "紧急退出 · Esc";
            Footnote.Text = "不适请停止活动 · 结束后自动返回";
        }
        Loaded += (_, _) =>
        {
            WindowPlacement.Place(this, strict, !strict && kind == BreakKind.Eyes);
            if (strict)
            {
                BeginSession();
                var primary = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
                foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                {
                    if (screen.DeviceName == primary.DeviceName) continue;
                    var cover = new RestCoverWindow(screen, RequestExit) { Owner = this };
                    _covers.Add(cover); cover.Show();
                }
                Render(); Activate();
            }
        };
        Closing += (_, e) =>
        {
            if (_strict && !_allowClose && !_session.Finished) { e.Cancel = true; RequestExit(); }
            else { foreach (var cover in _covers) cover.Release(); _covers.Clear(); }
        };
        Closed += (_, _) =>
        {
            foreach (var cover in _covers) cover.Release();
            _covers.Clear(); Ended?.Invoke(_session);
        };
        RestArt.Source = RestArtwork.For(kind == BreakKind.Eyes ? RestScene.Distance : RestScene.Movement);
        RenderJourney();
        RenderRoute();
        SizeChanged += (_, _) => FitScene();
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) PauseForInterruption(); };
    }

    private void Start_Click(object sender, RoutedEventArgs e) => BeginSession();

    public void BeginSession()
    {
        if (_started) return;
        _started = true;
        _routeIndex = -1;
        RestArt.Visibility = Visibility.Visible;
        StateBadge.Visibility = Visibility.Collapsed;
        NextStep.Visibility = Visibility.Collapsed;
        StepLabel.Visibility = Visibility.Collapsed;
        Countdown.FontSize = 48;
        TimerCaption.Text = "当前环节剩余";
        if (_strict || _session.Kind != BreakKind.Eyes) WindowPlacement.Place(this, true, false);
        StartButton.Visibility = Visibility.Collapsed;
        SnoozeButton.Visibility = Visibility.Collapsed;
        PauseButton.Visibility = _strict ? Visibility.Collapsed : Visibility.Visible;
        SkipButton.Visibility = _strict || _session.Kind == BreakKind.Eyes ? Visibility.Collapsed : Visibility.Visible;
        AlternativeButton.Visibility = _strict && _session.Kind != BreakKind.Eyes ? Visibility.Visible : Visibility.Collapsed;
        Progress.Visibility = Visibility.Collapsed;
        Render();
        Motion.Reveal(SceneContent);
        if (_strict) ExitButton.Focus(); else PauseButton.Focus();
    }

    public void Tick(TimeSpan elapsed) { _session.Advance(elapsed); Render(); }
    public void PauseForInterruption()
    {
        if (!_started || _session.Finished) return;
        _session.Paused = true;
        Render();
    }
    private void Pause_Click(object sender, RoutedEventArgs e)
    { if (!_strict || _session.Paused) { _session.Paused = !_session.Paused; Render(); } }
    private void Skip_Click(object sender, RoutedEventArgs e) { _session.Skip(); Render(); }
    private void Exit_Click(object sender, RoutedEventArgs e) => RequestExit();
    public void CloseForSystem() { _allowClose = true; Close(); }
    private void RequestExit()
    {
        if (!_strict || _session.Finished) { Close(); return; }
        EmergencyPanel.Visibility = Visibility.Visible;
        Motion.Reveal(EmergencyPanel);
        Activate(); ContinueButton.Focus();
    }
    private void ConfirmExit_Click(object sender, RoutedEventArgs e) => CloseForSystem();
    private void Continue_Click(object sender, RoutedEventArgs e)
    { EmergencyPanel.Visibility = Visibility.Collapsed; ExitButton.Focus(); }
    private void Alternative_Click(object sender, RoutedEventArgs e)
    { _session.UseRestAlternative(); AlternativeButton.Visibility = Visibility.Collapsed; Render(); }
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { RequestExit(); e.Handled = true; }
        else if (e.Key == Key.Space && e.OriginalSource is not System.Windows.Controls.Button)
        { if (!_started) Start_Click(sender, e); else if (!_session.Finished) Pause_Click(sender, e); e.Handled = true; }
    }

    private void Render()
    {
        RenderJourney();
        if (_session.Finished)
        {
            if (_finishing) return;
            _finishing = true;
            if (_session.FullyCompleted) Completed?.Invoke(_session);
            if (_strict || _session.Kind == BreakKind.Eyes)
            {
                if (_session.FullyCompleted && _sound) System.Media.SystemSounds.Asterisk.Play();
                Close();
                return;
            }
            if (_session.FullyCompleted && _sound) System.Media.SystemSounds.Asterisk.Play();
            RestArt.Source = RestArtwork.For(RestScene.Distance);
            RestArt.Visibility = Visibility.Visible;
            StateBadge.Visibility = Visibility.Visible;
            Countdown.FontSize = 44;
            SessionRing.Value = _session.Observed.TotalSeconds / Programs.Duration(_session.Kind).TotalSeconds;
            CompletionSymbol.Text = _session.FullyCompleted ? "\uE8FB" : "\uE823";
            RoutePanel.Visibility = Visibility.Collapsed;
            SceneName.Text = _session.FullyCompleted ? "休息结束，节奏继续" : "按照自己的节奏来";
            SceneNote.Text = "每一次停下来，都可以从容一些。";
            TimerCaption.Text = "已留给自己的时间";
            Motion.Reveal(SceneContent);
            NextStep.Visibility = Visibility.Collapsed;
            Visuals.SetGlyph(SnoozeButton, "\uE72C");
            NextStep.Text = "";
            Heading.Text = _session.FullyCompleted ? "好了，带着轻松回来" : "这次就到这里";
            Instruction.Text = _session.FullyCompleted ? "活动流程已完成。准备好了，再继续工作。" : "你跳过了部分动作，这次不会记为完整活动。";
            Countdown.Text = _session.Observed.ToString(@"mm\:ss");
            PauseButton.Visibility = SkipButton.Visibility = Visibility.Collapsed;
            SnoozeButton.Content = "回到工作";
            SnoozeButton.Visibility = Visibility.Visible;
            Progress.Value = SessionRing.Value * 100;
            StepLabel.Text = "不必追求打卡，舒服地活动就好。";
            return;
        }
        RenderRoute();
        var exercise = _session.Current;
        Eyebrow.Text = _session.Paused ? "已暂停 · 准备好后继续" : _strict ? "强制休息中 · 这段时间留给自己" : "给身体一点空间";
        Heading.Text = _session.RestOnly ? "现在，安静休息一下" : exercise.Title;
        Instruction.Text = _session.RestOnly ? "停止当前动作，选择舒适、有支撑的姿势。把目光移开屏幕，剩余时间继续休息。" : exercise.Instruction;
        ApplyScene(_session.RestOnly ? RestScene.Distance : exercise.Scene);
        if (_strict)
        {
            PauseButton.Visibility = _session.Paused ? Visibility.Visible : Visibility.Collapsed;
        }
        if (_lastIndex != _session.Index) { Motion.Reveal(Heading); Motion.Reveal(Instruction); }
        if (_lastIndex != _session.Index && _sound && !_session.RestOnly) System.Media.SystemSounds.Asterisk.Play();
        _lastIndex = _session.Index;
        NextStep.Text = _session.Index + 1 < _session.Exercises.Count ? "接下来 · " + _session.Exercises[_session.Index + 1].Title : "这是最后一个动作";
        if (_strict && _session.Kind == BreakKind.Eyes) NextStep.Text = "不必盯着屏幕 · 结束时自动返回";
        if (_session.RestOnly) NextStep.Text = "无需继续动作 · 安静休息不记为完整身体活动";
        Countdown.Text = TimeSpan.FromSeconds(Math.Ceiling((_session.RestOnly
            ? _session.TotalRemaining : _session.Remaining).TotalSeconds)).ToString(@"mm\:ss");
        Progress.Value = _session.RestOnly ? 100 * _session.Observed.TotalSeconds / Programs.Duration(_session.Kind).TotalSeconds : 100 * _session.StepElapsed.TotalSeconds / exercise.Seconds;
        SessionRing.Value = Progress.Value / 100;
        TimerCaption.Text = _session.Paused ? "已暂停" : _session.RestOnly ? "安静休息剩余" : "当前环节剩余";
        PauseButton.Content = _session.Paused ? "继续" : "暂停";
        Visuals.SetGlyph(PauseButton, _session.Paused ? "\uE768" : "\uE769");
        StepLabel.Text = _strict
            ? $"{_session.Index + 1} / {_session.Exercises.Count} · 还有 {Math.Ceiling((Programs.Duration(_session.Kind) - _session.Observed).TotalSeconds)} 秒自动返回"
            : $"{_session.Index + 1} / {_session.Exercises.Count} · 已活动 {(int)_session.Observed.TotalSeconds} 秒";
        foreach (var cover in _covers) cover.Update(Heading.Text, TotalCountdown.Text, Background, RestArt.Source, TotalProgress.Value);
    }

    private void RenderJourney()
    {
        var total = Programs.Duration(_session.Kind).TotalSeconds;
        var finished = _session.Finished;
        var elapsed = finished ? _session.Observed.TotalSeconds : _session.TimelineElapsed.TotalSeconds;
        TotalProgress.Value = Math.Clamp(100 * elapsed / total, 0, 100);
        var remaining = TimeSpan.FromSeconds(Math.Ceiling(_session.TotalRemaining.TotalSeconds));
        TotalCountdown.Text = finished ? $"实际休息 {_session.Observed:mm\\:ss}" : $"总剩余 {remaining:mm\\:ss}";
        JourneyLabel.Text = finished ? (_session.FullyCompleted ? "本次休息已完成" : "流程结束 · 未完整完成")
            : _session.RestOnly ? "安静休息" : $"动作 {_session.Index + 1} / {_session.Exercises.Count}";
        JourneyNote.Text = finished ? "以上进度按实际休息时长显示。"
            : _session.Paused ? "已暂停 · 当前环节与整场计时均已停止"
            : !_started ? $"预计 {Programs.Duration(_session.Kind):mm\\:ss} · 准备好后开始"
            : _session.TimelineElapsed > _session.Observed ? $"已跳过部分动作 · 实际休息 {_session.Observed:mm\\:ss} · 不记为完整活动"
            : _strict ? "结束后自动返回工作" : "按自己的节奏来 · 环节切换时总进度会继续前进";
        JourneyPanel.Visibility = _session.Kind == BreakKind.Eyes ? Visibility.Collapsed : Visibility.Visible;
        JourneyNote.Visibility = _session.Paused || (finished && !_session.FullyCompleted) || _session.TimelineElapsed > _session.Observed
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RenderRoute()
    {
        RoutePanel.Visibility = ActualHeight < 860 || _session.Kind == BreakKind.Eyes || _session.RestOnly || _session.Finished ? Visibility.Collapsed : Visibility.Visible;
        if (_routeIndex == _session.Index) return;
        _routeIndex = _session.Index;
        RouteHeading.Text = _started ? "此刻与接下来" : "活动预告 · 前三个环节";
        RouteItems.ItemsSource = _session.Exercises.Skip(_session.Index).Take(3).Select((exercise, index) => new
        {
            Title = (_started && index == 0 ? "现在 · " : "") + exercise.Title,
            Duration = $"{exercise.Seconds} 秒",
            Weight = _started && index == 0 ? FontWeights.SemiBold : FontWeights.Normal
        }).ToArray();
    }

    private void FitScene()
    {
        RenderRoute();
        var wide = ActualWidth >= 760;
        ScenicPanel.Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
        SceneryColumn.Width = wide ? new GridLength(.85, GridUnitType.Star) : new GridLength(0);
        var compact = ActualHeight < 860;
        TimerFace.Width = 280;
        TimerFace.Height = compact ? 140 : 170;
        Heading.FontSize = wide ? 38 : 30;
        Countdown.FontSize = compact ? 68 : 80;
    }

    private void ApplyScene(RestScene scene)
    {
        if (_lastScene == scene) return;
        _lastScene = scene;
        var color = scene switch
        {
            RestScene.Distance => "#E9F1F5", RestScene.Shoulders => "#F7EDDF",
            RestScene.Hands => "#EFEAF4", _ => "#EBF1E5"
        };
        var previous = (Background as SolidColorBrush)?.Color;
        var next = (Color)ColorConverter.ConvertFromString(color);
        var brush = new SolidColorBrush(next); Background = brush;
        Motion.TrackClock(brush, SolidColorBrush.ColorProperty);
        if (Motion.Enabled && previous is { } from)
            brush.BeginAnimation(SolidColorBrush.ColorProperty, new System.Windows.Media.Animation.ColorAnimation(from, next, TimeSpan.FromMilliseconds(600)) { FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop });
        RestArt.Source = RestArtwork.For(scene);
        RestArt.Visibility = Visibility.Visible;
        StateBadge.Visibility = Visibility.Collapsed;
        (SceneName.Text, SceneNote.Text) = scene switch
        {
            RestScene.Distance => ("让目光，越过屏幕", "真正的远眺，在屏幕之外。"),
            RestScene.Shoulders => ("松开肩膀，也松口气", "慢一点，只到舒服的范围。"),
            RestScene.Hands => ("把紧绷，轻轻放下", "手离开键盘，身体回到此刻。"),
            _ => ("离开座位，换个风景", "几步路，也是工作日的留白。")
        };
        Motion.Reveal(SceneName);
        FitScene();
    }

}
