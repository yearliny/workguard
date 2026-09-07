using WorkGuard.Data;

namespace WorkGuard.Windows.Views;

public partial class DashboardWindow : Window
{
    private readonly AppController _app;
    private DateTime _lastWeekRefresh = DateTime.MinValue;
    internal DashboardWindow(AppController app)
    {
        InitializeComponent();
        _app = app;
        HeroArt.SizeChanged += (_, _) => HeroArt.Clip = new RectangleGeometry(new Rect(0, 0, HeroArt.ActualWidth, HeroArt.ActualHeight), 20, 20);
        _app.Changed += Refresh;
        Closed += (_, _) => _app.Changed -= Refresh;
        Refresh();
    }
    private static string Time(double seconds) => $"{(int)(seconds / 3600)}h {(int)(seconds % 3600 / 60):00}m";
    private void Refresh()
    {
        var today = LocalStore.Day(_app.State, DateOnly.FromDateTime(DateTime.Now));
        StatusText.Text = (_app.State.Preferences.StrictMode ? "强制模式 · " : "温和模式 · ") + _app.Status;
        GreetingText.Text = DateTime.Now.Hour switch { < 11 => "新的一天，从容开始。", < 17 => "忙碌之间，留一点空隙。", _ => "辛苦了，也照顾好自己。" };
        WorkRing.Value = _app.Engine.Continuous.TotalMinutes / _app.State.Preferences.MovementIntervalMinutes;
        ErrorBanner.Visibility = _app.DataError is null ? Visibility.Collapsed : Visibility.Visible;
        ErrorText.Text = _app.DataError;
        RestoreButton.IsEnabled = _app.CanRestoreBackup;
        TimingHint.Text = _app.Paused ? $"约 {Math.Ceiling(_app.PauseRemaining.TotalMinutes)} 分钟后自动恢复" :
            _app.Engine.SnoozeRemaining > TimeSpan.Zero ? $"提醒已推迟 {Math.Ceiling(_app.Engine.SnoozeRemaining.TotalMinutes)} 分钟" :
            _app.State.Preferences.EyeReminders ? $"下次远眺约 {Math.Ceiling(_app.Engine.EyeDueIn.TotalMinutes)} 分钟后" : "眼睛提醒已关闭";
        ContinuousText.Text = ((int)_app.Engine.Continuous.TotalMinutes).ToString("00");
        NextText.Text = _app.Engine.MovementDueIn > TimeSpan.Zero ? $"约 {Math.Ceiling(_app.Engine.MovementDueIn.TotalMinutes)} 分钟后，起来走一走。" : "已经到了活动时间，给身体几分钟。";
        ActiveText.Text = Time(today.ActiveSeconds);
        LongestText.Text = $"{(int)(today.LongestSeconds / 60)} 分钟";
        BreaksText.Text = $"{today.MovementBreaks + today.OfficeBreaks} 次";
        EyeText.Text = $"眼睛休息 {today.EyeBreaks} 次 · 7 分钟办公室活动 {today.OfficeBreaks} 次";
        PauseButton.Content = _app.Paused ? "恢复提醒" : "暂停提醒 1 小时";
        Visuals.SetGlyph(PauseButton, _app.Paused ? "\uE768" : "\uE769");
        if ((DateTime.Now - _lastWeekRefresh).Duration() < TimeSpan.FromSeconds(10)) return;
        _lastWeekRefresh = DateTime.Now;
        var chartDays = Enumerable.Range(0, 7).Select(i =>
        {
            var date = today.Date.AddDays(i - 6);
            return (date, day: _app.State.Days.FirstOrDefault(d => d.Date == date));
        }).ToArray();
        var max = Math.Max(1, chartDays.Max(x => x.day?.LongestSeconds ?? 0));
        WeekBars.ItemsSource = chartDays.Select(x => new
        {
            Date = x.date.ToString("MM-dd"), Label = x.day is null ? "—" : ((int)(x.day.LongestSeconds / 60)).ToString(),
            Height = (x.day?.LongestSeconds ?? 0) / max * 60,
            Description = x.day is null ? $"{x.date:MM-dd}：暂无记录" : $"{x.date:MM-dd}：最长连续工作 {(int)(x.day.LongestSeconds / 60)} 分钟"
        });
        WeekGrid.ItemsSource = Enumerable.Range(0, 7).Select(i =>
        {
            var date = today.Date.AddDays(-i);
            var day = _app.State.Days.FirstOrDefault(d => d.Date == date);
            return new { Date = date.ToString("MM-dd"), Active = day is null ? "—" : Time(day.ActiveSeconds),
                Longest = day is null ? "—" : $"{(int)(day.LongestSeconds / 60)} 分钟",
                Breaks = day is null ? "—" : $"{day.MovementBreaks + day.OfficeBreaks} / {day.EyeBreaks}" };
        }).ToArray();
    }
    private void Restore_Click(object sender, RoutedEventArgs e) => _app.RestoreBackup();
    private void Export_Click(object sender, RoutedEventArgs e) => _app.ExportCsv();
    private void Settings_Click(object sender, RoutedEventArgs e) => _app.ShowSettings();
    private void Movement_Click(object sender, RoutedEventArgs e) => _app.StartBreak(BreakKind.Movement);
    private void Eyes_Click(object sender, RoutedEventArgs e) => _app.StartBreak(BreakKind.Eyes);
    private void Office_Click(object sender, RoutedEventArgs e) => _app.StartBreak(BreakKind.Office);
    private void Pause_Click(object sender, RoutedEventArgs e) => _app.TogglePause();
}
