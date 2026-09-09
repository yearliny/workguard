using System.Windows.Input;
using WorkGuard.Windows.Platform;

namespace WorkGuard.Windows.Views;

public partial class MaintenanceWindow : Window
{
    internal MaintenanceSession Session { get; }
    private readonly bool _strict;
    private bool _voiceEnabled, _allowClose, _completionShown;
    private bool _resumeAfterExit, _previewPlaying;
    private int _previewSide = 1;
    private readonly MaintenanceVoice _voice = new();
    private readonly List<RestCoverWindow> _covers = [];
    private readonly Func<MaintenanceExercise, MaintenanceExercise?> _replacement;
    private readonly Action<string> _block;
    private string _spoken = "";
    public bool HasStarted { get; private set; }
    public event Action<MaintenanceSession, IReadOnlyList<MaintenanceCredit>>? Confirmed;
    public event Action? Ended;
    internal MaintenanceWindow(IReadOnlyList<MaintenanceStep> plan, bool strict, bool voice,
        Func<MaintenanceExercise, MaintenanceExercise?> replacement, Action<string> block)
    {
        InitializeComponent();
        Session = new(plan); _strict = strict; _voiceEnabled = voice; _replacement = replacement; _block = block;
        Countdown.Text = Clock(Session.TotalSeconds);
        TotalTime.Text = "预计 " + Clock(Session.TotalSeconds) + " · 含准备";
        StepCount.Text = plan.Count + " 个动作 · 按自己的节奏来";
        PreviewSelector.ItemsSource = plan.Select((step, index) => new
        { Index = index, Label = $"{index + 1}. {step.Exercise.Title} · {Clock(step.Seconds)}" }).ToArray();
        PreviewSelector.SelectedIndex = 0;
        VoiceButton.Content = voice ? "语音开启" : "语音关闭";
        Loaded += (_, _) => { if (_strict) BeginSession(); };
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) PauseForInterruption(); };
        Closing += (_, e) =>
        {
            if (_strict && !_allowClose && !Session.Finished) { e.Cancel = true; RequestExit(); }
        };
        Closed += (_, _) => { ReleaseCovers(); _voice.Dispose(); Ended?.Invoke(); };
        SizeChanged += (_, _) => FitContent();
    }
    private void FitContent()
    {
        Demonstration.Height = !HasStarted ? 180 : ActualHeight < 720 ? 160 : ActualHeight < 860 ? 240 : 300;
        Countdown.FontSize = ActualWidth < 800 ? 48 : 64; Heading.FontSize = ActualWidth < 800 ? 28 : 34;
    }
    private void RenderPreview()
    {
        if (HasStarted || PreviewSelector.SelectedIndex < 0) return;
        var index = PreviewSelector.SelectedIndex;
        var exercise = Session.Steps[index].Exercise;
        Heading.Text = exercise.Title;
        Phase.Text = "动作预览 · " + MaintenanceCatalog.Label(exercise.Area) +
            (exercise.Bilateral ? (_previewSide == 1 ? " · 左侧" : " · 右侧") : "");
        Cue.Text = exercise.Setup + "\n" + exercise.Cue;
        Next.Text = "开始后先做「" + Session.Steps[0].Exercise.Title + "」";
        PreviewSideButton.Visibility = exercise.Bilateral ? Visibility.Visible : Visibility.Collapsed;
        PreviewSideButton.Content = _previewSide == 1 ? "看看右侧" : "看看左侧";
        PreviewMotionButton.Content = _previewPlaying && Motion.Enabled ? "暂停示意" : "播放示意";
        PreviewMotionButton.IsEnabled = Motion.Enabled;
        StatusNote.Text = Motion.Enabled ? "预览不计入跟练 · 左右侧以你自己为准" : "已减少动态效果，显示静态动作位置 · 预览不计入跟练";
        Demonstration.Set(exercise, _previewPlaying, _previewSide);
        FitContent();
    }
    private void Preview_Changed(object sender, SelectionChangedEventArgs e)
    { _previewSide = 1; RenderPreview(); }
    private void PreviewMotion_Click(object sender, RoutedEventArgs e)
    { if (!HasStarted) { _previewPlaying = !_previewPlaying; RenderPreview(); } }
    private void PreviewSide_Click(object sender, RoutedEventArgs e)
    { if (!HasStarted) { _previewSide = _previewSide == 1 ? 2 : 1; RenderPreview(); } }
    private static string Clock(double seconds) => TimeSpan.FromSeconds(Math.Ceiling(Math.Max(0, seconds))).ToString(@"mm\:ss");
    public void BeginSession()
    {
        if (HasStarted) return;
        HasStarted = true; WindowStyle = WindowStyle.None; Topmost = true;
        WindowPlacement.Place(this, true, false);
        if (_strict)
        {
            var primary = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                if (screen.DeviceName != primary.DeviceName)
                {
                    var cover = new RestCoverWindow(screen, CloseForSystem) { Owner = this };
                    _covers.Add(cover); cover.Show();
                }
        }
        StartButton.Visibility = PreviewPanel.Visibility = PreviewMotionButton.Visibility = PreviewSideButton.Visibility = Visibility.Collapsed;
        VoiceButton.Visibility = Visibility.Visible;
        StatusNote.Text = "动作以舒适为准 · 疼痛、麻木或头晕时，请停止";
        ReplaceButton.Visibility = DiscomfortButton.Visibility = Visibility.Visible;
        SkipButton.Visibility = _strict ? Visibility.Collapsed : Visibility.Visible;
        FitContent(); Render(); DiscomfortButton.Focus();
    }
    public void Tick(TimeSpan elapsed, DateTimeOffset end)
    {
        if (!HasStarted) return;
        if (elapsed > TimeSpan.FromSeconds(10)) PauseForInterruption();
        else Session.Advance(elapsed, end);
        Render();
    }
    public void PauseForInterruption()
    {
        if (!HasStarted) { _previewPlaying = false; RenderPreview(); return; }
        if (Session.Finished) return;
        _resumeAfterExit = false;
        Session.Paused = true; _voice.Stop(); Render();
    }
    private void Render()
    {
        TotalProgress.Value = 100 * Session.TimelineSeconds / Session.TotalSeconds;
        TotalTime.Text = "本次还剩 " + Clock(Session.TotalRemaining);
        if (Session.Finished)
        {
            if (_completionShown) return;
            _completionShown = true; _voice.Stop(); ReleaseCovers();
            // Completion must release the forced fullscreen even before confirmation.
            Topmost = false; WindowStyle = WindowStyle.SingleBorderWindow; WindowState = WindowState.Normal;
            Width = 860; Height = 740; WindowPlacement.Place(this, false, false, topmost: false);
            PracticePanel.Visibility = Controls.Visibility = Journey.Visibility = EmergencyPanel.Visibility = Visibility.Collapsed;
            ConfirmationPanel.Visibility = Visibility.Visible;
            Demonstration.Set(Session.Current.Exercise, false);
            ConfirmationSummary.Text = "引导中的实际活动时间 " + Clock(Session.ObservedPractice) + "（不含准备和暂停）";
            for (var i = 0; i < Session.Steps.Count; i++)
                if (Session.PracticedAt(i) > 0)
                    ConfirmationChoices.Children.Add(new CheckBox { Tag = i, IsChecked = true,
                        Content = Session.Steps[i].Exercise.Title + " · " + Clock(Session.PracticedAt(i)), Margin = new Thickness(0, 5, 0, 5) });
            ConfirmButton.IsEnabled = ConfirmationChoices.Children.Count > 0;
            StatusNote.Text = "记录来自你的确认，不代表动作质量或健康改善。";
            ConfirmButton.Focus(); return;
        }
        var exercise = Session.Current.Exercise;
        Heading.Text = exercise.Title;
        Phase.Text = EmergencyPanel.Visibility == Visibility.Visible ? "计时已暂停 · 等待你的选择" :
            Session.Paused ? "已暂停 · 准备好后继续" : MaintenanceCatalog.Label(exercise.Area) + " · " + Session.Phase;
        Cue.Text = Session.Preparing ? exercise.Setup : exercise.Cue;
        var remaining = Session.Preparing ? MaintenanceStep.PreparationSeconds - Session.StepElapsed :
            exercise.Bilateral && Session.Side == 1 ? Session.Current.Seconds / 2.0 - Session.PracticeElapsed : Session.Current.TotalSeconds - Session.StepElapsed;
        Countdown.Text = Clock(remaining);
        TimerLabel.Text = Session.Paused ? "计时已暂停" : Session.Preparing ? "准备姿势剩余" : exercise.Bilateral ? "当前侧剩余" : "当前动作剩余";
        StepCount.Text = $"动作 {Session.Index + 1} / {Session.Steps.Count}";
        Next.Text = exercise.Bilateral && Session.Side == 1 ? "接下来 · " + (Session.Preparing ? "从左侧开始，再换右侧" : "慢慢回正，换右侧") :
            Session.Index + 1 < Session.Steps.Count ? "接下来 · " + Session.Steps[Session.Index + 1].Exercise.Title : "最后一个动作 · 完成后确认跟练";
        PauseButton.Visibility = (!_strict || Session.Paused) && EmergencyPanel.Visibility != Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
        PauseButton.Content = Session.Paused ? "继续" : "暂停";
        Demonstration.Set(exercise, !Session.Paused && !Session.Preparing, Session.Side);
        foreach (var cover in _covers) cover.Update(Heading.Text, TotalTime.Text, Background, progress: TotalProgress.Value);
        var spoken = $"{Session.Index}:{exercise.Id}:{Session.Phase}:{Session.Paused}";
        if (_spoken != spoken)
        {
            _spoken = spoken;
            if (_voiceEnabled && !Session.Paused)
            {
                _voice.Speak(Session.Preparing ? exercise.Title + "。" + exercise.Setup :
                    exercise.Bilateral && Session.Side == 2 ? "慢慢回正。现在换右侧。" : (exercise.Bilateral ? "从左侧开始。" : "现在开始。") + exercise.Cue);
                if (!_voice.Available) StatusNote.Text = "未找到可用的中文系统语音，请跟随文字与示意。出现不适请停止。";
            }
        }
    }
    public void CloseForSystem() { _allowClose = true; Close(); }
    private void ReleaseCovers() { foreach (var cover in _covers) cover.Release(); _covers.Clear(); }
    private void RequestExit()
    {
        if (!_strict || Session.Finished) { CloseForSystem(); return; }
        if (EmergencyPanel.Visibility == Visibility.Visible) { ConfirmExitButton.Focus(); return; }
        _resumeAfterExit = !Session.Paused;
        Session.Paused = true;
        EmergencyPanel.Visibility = Visibility.Visible; _voice.Stop();
        ReplaceButton.IsEnabled = VoiceButton.IsEnabled = false;
        Render(); ConfirmExitButton.Focus();
    }
    private void Start_Click(object sender, RoutedEventArgs e) => BeginSession();
    private void Pause_Click(object sender, RoutedEventArgs e)
    { if (EmergencyPanel.Visibility != Visibility.Visible && (!_strict || Session.Paused)) { Session.Paused = !Session.Paused; if (Session.Paused) _voice.Stop(); Render(); } }
    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        if (EmergencyPanel.Visibility == Visibility.Visible) return;
        var replacement = _replacement(Session.Current.Exercise);
        if (replacement is null) { StatusNote.Text = "暂无合适替代，不适请立即停止。"; return; }
        Session.Replace(replacement); Render();
    }
    private void Skip_Click(object sender, RoutedEventArgs e) { if (!_strict) { Session.Skip(); Render(); } }
    private void Discomfort_Click(object sender, RoutedEventArgs e)
    {
        var id = Session.Current.Exercise.Id;
        _voice.Stop(); Session.Stop(); CloseForSystem(); _block(id);
    }
    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        var selected = ConfirmationChoices.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (int)c.Tag).ToArray();
        Confirmed?.Invoke(Session, Session.Confirm(selected)); CloseForSystem();
    }
    private void NoPractice_Click(object sender, RoutedEventArgs e) { Session.Confirm([]); CloseForSystem(); }
    private void Voice_Click(object sender, RoutedEventArgs e)
    { _voiceEnabled = !_voiceEnabled; VoiceButton.Content = _voiceEnabled ? "语音开启" : "语音关闭"; _voice.Stop(); _spoken = ""; if (HasStarted && !Session.Finished) Render(); }
    private void Exit_Click(object sender, RoutedEventArgs e) => RequestExit();
    private void ConfirmExit_Click(object sender, RoutedEventArgs e) => CloseForSystem();
    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (EmergencyPanel.Visibility != Visibility.Visible) return;
        EmergencyPanel.Visibility = Visibility.Collapsed;
        Session.Paused = !_resumeAfterExit; _resumeAfterExit = false;
        ReplaceButton.IsEnabled = VoiceButton.IsEnabled = true;
        _spoken = ""; Render();
        if (Session.Paused) PauseButton.Focus(); else DiscomfortButton.Focus();
    }
    private void Window_KeyDown(object sender, KeyEventArgs e)
    { if (e.Key == Key.Escape) { RequestExit(); e.Handled = true; } }
}
