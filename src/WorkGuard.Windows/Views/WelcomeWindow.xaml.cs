namespace WorkGuard.Windows.Views;

public partial class WelcomeWindow : Window
{
    private readonly Preferences _initial;
    private readonly Func<Preferences, bool> _save;
    private readonly Func<string?> _error;
    private readonly Action _finished;
    private int _page;
    internal WelcomeWindow(Preferences initial, Func<Preferences, bool> save, Func<string?> error, Action finished)
    {
        InitializeComponent(); _initial = initial; _save = save; _error = error; _finished = finished;
        KeepRhythm.IsChecked = true;
        CurrentRhythmText.Text = $"远眺间隔 {initial.EyeIntervalMinutes} 分钟 · 活动间隔 {initial.MovementIntervalMinutes} 分钟";
        if (!initial.OnboardingComplete && initial.EyeIntervalMinutes == 20 && initial.MovementIntervalMinutes == 60)
        { KeepRhythm.Visibility = Visibility.Collapsed; FocusRhythm.IsChecked = true; }
        StrictMode.IsChecked = initial.StrictMode; GentleMode.IsChecked = !initial.StrictMode;
        SoundEnabled.IsChecked = initial.SoundEnabled;
        SizeChanged += (_, _) => { var wide = ActualWidth >= 780; ArtPanel.Visibility = wide ? Visibility.Visible : Visibility.Collapsed; ArtColumn.Width = wide ? new GridLength(.65, GridUnitType.Star) : new GridLength(0); };
        RenderPage();
    }
    private Preferences Selection() => new QuickSetup(DailyRhythm.IsChecked == true ? 50 : FocusRhythm.IsChecked == true ? 60 : null,
        StrictMode.IsChecked == true, SoundEnabled.IsChecked == true).ApplyTo(_initial);
    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_page < 2) { _page++; RenderPage(); return; }
        if (!_save(Selection())) { ErrorText.Text = _error() ?? "设置未保存，请检查后重试。"; return; }
        Close(); _finished();
    }
    private void Back_Click(object sender, RoutedEventArgs e) { if (_page > 0) { _page--; RenderPage(); } }
    private void RenderPage()
    {
        RhythmPage.Visibility = _page == 0 ? Visibility.Visible : Visibility.Collapsed;
        ModePage.Visibility = _page == 1 ? Visibility.Visible : Visibility.Collapsed;
        ReadyPage.Visibility = _page == 2 ? Visibility.Visible : Visibility.Collapsed;
        BackButton.Visibility = _page == 0 ? Visibility.Hidden : Visibility.Visible;
        StepText.Text = $"{_page + 1} / 3 · 随时可以返回修改";
        PageTitle.Text = _page switch { 0 => "选一个舒服的节奏", 1 => "你希望怎样被提醒？", _ => "准备好了，从容开始" };
        PageHint.Text = _page switch { 0 => "不必一次设置所有细节，先选一个起点。", 1 => "力度可以不同，休息都以舒适为准。", _ => "保存后生效，其余偏好保持原样。" };
        NextButton.Content = _page == 2 ? "保存并开始" : "下一步";
        var p = Selection();
        ScheduleText.Text = (p.EyeReminders ? $"每 {p.EyeIntervalMinutes} 分钟 · 远眺 20 秒" : "眼睛休息提醒已关闭") + $"\n每 {p.MovementIntervalMinutes} 分钟 · 活动 3 分钟";
        ModeText.Text = p.StrictMode ? "身体活动直接全屏" + (p.ShouldForce(BreakKind.Eyes) ? "，眼睛休息也全屏。" : "；眼睛休息遵循原有设置。") : "温和提醒 · 准备好后再开始。";
        ErrorText.Text = "";
        Motion.Reveal(Pages);
    }
}
