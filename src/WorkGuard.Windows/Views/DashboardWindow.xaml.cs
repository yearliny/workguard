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
        _app.Changed += Refresh;
        Closed += (_, _) => _app.Changed -= Refresh;
        Refresh();
    }
    private static string Time(double seconds) => $"{(int)(seconds / 3600)}h {(int)(seconds % 3600 / 60):00}m";
    private void Refresh()
    {
        var today = LocalStore.Day(_app.State, DateOnly.FromDateTime(DateTime.Now));
        StatusText.Text = _app.Status;
        ContinuousText.Text = ((int)_app.Engine.Continuous.TotalMinutes).ToString("00");
        NextText.Text = _app.Engine.MovementDueIn > TimeSpan.Zero ? $"约 {Math.Ceiling(_app.Engine.MovementDueIn.TotalMinutes)} 分钟后，起来走一走。" : "已经到了活动时间，给身体几分钟。";
        ActiveText.Text = Time(today.ActiveSeconds);
        LongestText.Text = $"{(int)(today.LongestSeconds / 60)} 分钟";
        BreaksText.Text = $"{today.MovementBreaks + today.OfficeBreaks} 次";
        EyeText.Text = $"眼睛休息 {today.EyeBreaks} 次 · 7 分钟办公室活动 {today.OfficeBreaks} 次";
        PauseButton.Content = _app.Paused ? "恢复提醒" : "暂停提醒 1 小时";
        if ((DateTime.Now - _lastWeekRefresh).Duration() < TimeSpan.FromSeconds(10)) return;
        _lastWeekRefresh = DateTime.Now;
        WeekGrid.ItemsSource = Enumerable.Range(0, 7).Select(i =>
        {
            var date = today.Date.AddDays(-i);
            var day = _app.State.Days.FirstOrDefault(d => d.Date == date);
            return new { Date = date.ToString("MM-dd"), Active = day is null ? "—" : Time(day.ActiveSeconds),
                Longest = day is null ? "—" : $"{(int)(day.LongestSeconds / 60)} 分钟",
                Breaks = day is null ? "—" : $"{day.MovementBreaks + day.OfficeBreaks} / {day.EyeBreaks}" };
        }).ToArray();
    }
    private void Settings_Click(object sender, RoutedEventArgs e) => _app.ShowSettings();
    private void Movement_Click(object sender, RoutedEventArgs e) => _app.StartBreak(BreakKind.Movement);
    private void Eyes_Click(object sender, RoutedEventArgs e) => _app.StartBreak(BreakKind.Eyes);
    private void Office_Click(object sender, RoutedEventArgs e) => _app.StartBreak(BreakKind.Office);
    private void Pause_Click(object sender, RoutedEventArgs e) => _app.TogglePause();
}
