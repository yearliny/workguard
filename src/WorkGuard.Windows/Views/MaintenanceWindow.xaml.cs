using System.Windows.Input;
using WorkGuard.Windows.Platform;

namespace WorkGuard.Windows.Views;

public partial class MaintenanceWindow : Window
{
    internal MaintenanceSession Session { get; }
    private readonly bool _strict;
    private bool _voiceEnabled, _allowClose, _completionShown;
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
        Cue.Text = "今天已经做过的内容会衔接。\n准备好后开始；任何动作不舒服，都可以停止。";
        Countdown.Text = Clock(Session.TotalSeconds);
        TotalTime.Text = "预计 " + Clock(Session.TotalSeconds) + " · 含准备";
        StepCount.Text = plan.Count + " 个动作 · 按自己的节奏来";
        Next.Text = "先从「" + plan[0].Exercise.Title + "」开始";
        Demonstration.Set(plan[0].Exercise, false);
        VoiceButton.Content = voice ? "语音开启" : "语音关闭";
        Loaded += (_, _) => { if (_strict) BeginSession(); };
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) PauseForInterruption(); };
        Closing += (_, e) =>
        {
            if (_strict && !_allowClose && !Session.Finished) { e.Cancel = true; RequestExit(); }
        };
        Closed += (_, _) => { ReleaseCovers(); _voice.Dispose(); Ended?.Invoke(); };
        SizeChanged += (_, _) => { Demonstration.Height = ActualHeight < 720 ? 200 : 270; Heading.FontSize = ActualWidth < 800 ? 28 : 34; };
    }
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
        StartButton.Visibility = Visibility.Collapsed;
        ReplaceButton.Visibility = DiscomfortButton.Visibility = Visibility.Visible;
        SkipButton.Visibility = _strict ? Visibility.Collapsed : Visibility.Visible;
        Render(); DiscomfortButton.Focus();
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
        if (!HasStarted || Session.Finished) return;
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
        Phase.Text = Session.Paused ? "已暂停 · 准备好后继续" : MaintenanceCatalog.Label(exercise.Area) + " · " + Session.Phase;
        Cue.Text = Session.Preparing ? exercise.Setup : exercise.Cue;
        var remaining = Session.Preparing ? MaintenanceStep.PreparationSeconds - Session.StepElapsed :
            exercise.Bilateral && Session.Side == 1 ? Session.Current.Seconds / 2.0 - Session.PracticeElapsed : Session.Current.TotalSeconds - Session.StepElapsed;
        Countdown.Text = Clock(remaining);
        TimerLabel.Text = Session.Paused ? "计时已暂停" : Session.Preparing ? "准备姿势剩余" : exercise.Bilateral ? "当前侧剩余" : "当前动作剩余";
        StepCount.Text = $"动作 {Session.Index + 1} / {Session.Steps.Count}";
        Next.Text = Session.Index + 1 < Session.Steps.Count ? "接下来 · " + Session.Steps[Session.Index + 1].Exercise.Title : "最后一个动作 · 完成后确认跟练";
        PauseButton.Visibility = !_strict || Session.Paused ? Visibility.Visible : Visibility.Collapsed;
        PauseButton.Content = Session.Paused ? "继续" : "暂停";
        Demonstration.Set(exercise, !Session.Paused && !Session.Preparing);
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
        EmergencyPanel.Visibility = Visibility.Visible; _voice.Stop(); ConfirmExitButton.Focus();
    }
    private void Start_Click(object sender, RoutedEventArgs e) => BeginSession();
    private void Pause_Click(object sender, RoutedEventArgs e)
    { if (!_strict || Session.Paused) { Session.Paused = !Session.Paused; if (Session.Paused) _voice.Stop(); Render(); } }
    private void Replace_Click(object sender, RoutedEventArgs e)
    {
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
    private void Continue_Click(object sender, RoutedEventArgs e) { EmergencyPanel.Visibility = Visibility.Collapsed; _spoken = ""; Render(); }
    private void Window_KeyDown(object sender, KeyEventArgs e)
    { if (e.Key == Key.Escape) { RequestExit(); e.Handled = true; } }
}
