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
        SizeChanged += (_, _) =>
        {
            var wide = ActualWidth >= 980;
            HeroImagePanel.Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
            HeroImageColumn.Width = new GridLength(wide ? 240 : 0);
        };
        _app.Changed += Refresh;
        Closed += (_, _) => _app.Changed -= Refresh;
        Refresh();
    }
    private static string Time(double seconds) => $"{(int)(seconds / 3600)}h {(int)(seconds % 3600 / 60):00}m";
    private void Refresh()
    {
        var localNow = _app.LocalNow;
        var today = LocalStore.Day(_app.State, DateOnly.FromDateTime(localNow));
        StatusText.Text = (_app.State.Preferences.StrictMode ? "强制模式 · " : "温和模式 · ") + _app.Status;
        GreetingText.Text = localNow.Hour switch { < 11 => "新的一天，从容开始。", < 17 => "忙碌之间，留一点空隙。", _ => "辛苦了，也照顾好自己。" };
        WorkRing.Value = _app.Engine.Continuous.TotalMinutes / _app.State.Preferences.MovementIntervalMinutes;
        ErrorBanner.Visibility = _app.DataError is null ? Visibility.Collapsed : Visibility.Visible;
        ErrorText.Text = _app.DataError;
        RestoreButton.IsEnabled = _app.CanRestoreBackup;
        TimingHint.Text = _app.Paused ? $"约 {Math.Ceiling(_app.PauseRemaining.TotalMinutes)} 分钟后自动恢复" :
            _app.Engine.SnoozeRemaining > TimeSpan.Zero ? $"提醒已推迟 {Math.Ceiling(_app.Engine.SnoozeRemaining.TotalMinutes)} 分钟" :
            !_app.State.Preferences.EyeReminders ? "眼睛提醒已关闭" : _app.Engine.EyeDueIn == TimeSpan.Zero ? "眼睛休息已到期" : $"下次远眺约 {Math.Ceiling(_app.Engine.EyeDueIn.TotalMinutes)} 分钟后";
        ContinuousText.Text = ((int)_app.Engine.Continuous.TotalMinutes).ToString("00");
        NextText.Text = _app.Engine.MovementDueIn > TimeSpan.Zero ? $"约 {Math.Ceiling(_app.Engine.MovementDueIn.TotalMinutes)} 分钟后，起来走一走。" : "已经到了活动时间，给身体几分钟。";
        if (!_app.Delivery.CanDeliver)
        {
            NextText.Text = _app.Delivery.Reason == DeliveryReason.Returning ? $"{Math.Ceiling(_app.Delivery.Remaining.TotalSeconds)} 秒后恢复到期提醒。" :
                _app.Delivery.Reason == DeliveryReason.Resting ? "休息时间，可以从活动窗口继续。" : "自动提醒暂缓，手动休息随时可用。";
            TimingHint.Text = _app.DeliveryDetail;
        }
        else if (_app.Engine.SnoozeRemaining > _app.Engine.MovementDueIn)
            NextText.Text = $"至少 {Math.Ceiling(_app.Engine.SnoozeRemaining.TotalMinutes)} 分钟后再提醒活动。";
        if (!_app.State.Preferences.OnboardingComplete) NextText.Text = "先完成快速配置，开启适合你的休息提醒。";
        if (_app.LastCompletion is not null) TimingHint.Text = _app.LastCompletion;
        var p = _app.State.Preferences;
        var confirmed = today.MaintenanceSeconds.Values.Sum();
        var plan = _app.MaintenancePlan(false);
        var fullPlan = _app.MaintenancePlan(true);
        var allowedAreas = MaintenanceCatalog.Allowed(p).Select(e => e.Area).ToHashSet();
        MaintenanceQuickSummary.Text = $"今日已确认 {confirmed / 60:0.#} / {p.MaintenanceGoalMinutes} 分钟";
        MaintenanceProgressText.Text = $"已确认跟练 {confirmed / 60:0.#} / {p.MaintenanceGoalMinutes} 分钟";
        MaintenanceProgress.Value = Math.Min(100, confirmed / (p.MaintenanceGoalMinutes * 60) * 100);
        MaintenanceNext.Text = plan.Count > 0 ? "接下来 · " + string.Join("、", plan.Select(x => MaintenanceCatalog.Label(x.Exercise.Area)).Distinct()) :
            allowedAreas.Count == 0 ? "当前没有可安排的动作，请在维护偏好中调整。" : "今日维护目标已完成。工作间隙仍记得走动与远望。";
        ShortMaintenanceButton.IsEnabled = FullMaintenanceButton.IsEnabled = plan.Count > 0;
        ShortMaintenanceButton.Content = plan.Count > 0 ? "短维护 · " + TimeSpan.FromSeconds(plan.Sum(x => x.TotalSeconds)).ToString(@"mm\:ss") : "短维护";
        FullMaintenanceButton.Content = fullPlan.Count > 0 ? "今日剩余 · " + TimeSpan.FromSeconds(fullPlan.Sum(x => x.TotalSeconds)).ToString(@"mm\:ss") : "今日已完成";
        MaintenanceScheduleNote.Text = (p.MaintenanceEnabled ? "短维护已融入身体休息提醒。" : "可手动开始；在维护偏好中开启自动安排。") + " 按钮时长包含准备。";
        MovementAction.Content = p.MaintenanceEnabled && plan.Count > 0 ? "现在做一段短维护" : p.UseFreeRest(BreakKind.Movement) ? "现在自由休息 3 分钟" : "现在活动 3 分钟";
        MaintenanceAreas.ItemsSource = new[] { BodyArea.Thoracic, BodyArea.Shoulders, BodyArea.Hips, BodyArea.BackLegs, BodyArea.Ankles, BodyArea.Neck }
            .Select(area => new { Name = MaintenanceCatalog.Label(area), Status = today.MaintenanceSeconds.GetValueOrDefault(area) > 0
                ? "已确认 " + TimeSpan.FromSeconds(today.MaintenanceSeconds[area]).ToString(@"mm\:ss") + (allowedAreas.Contains(area) ? "" : " · 目前不安排")
                : allowedAreas.Contains(area) ? "今天尚未跟练" : "不安排" }).ToArray();
        DeliveryTitle.Text = _app.Status;
        DeliveryExplanation.Text = _app.DeliveryDetail;
        ScheduleSummary.Text = p.WorkScheduleEnabled
            ? $"{ReminderCopy.Days(p)} · {ReminderCopy.Clock(p.WorkStartMinute)} 至 {(p.WorkEndMinute < p.WorkStartMinute ? "次日 " : "")}{ReminderCopy.Clock(p.WorkEndMinute)}"
            : "每天均可提醒 · 尚未限制工作时段";
        if (p.QuietHoursEnabled) ScheduleSummary.Text += $" · 安静 {ReminderCopy.Clock(p.QuietStartMinute)}–{ReminderCopy.Clock(p.QuietEndMinute)}";
        Timeline.Update(p, localNow);
        AppointmentSummary.Text = !p.OfficeReminderEnabled ? "尚未预约，随时可以手动开始。" :
            today.OfficeBreaks > 0 ? "今天已完成 7 分钟活动。" :
            _app.State.LastOfficeReminderDate > today.Date ? "系统日期已回拨，暂不重复发送邀请。" :
            _app.State.LastOfficeReminderDate == today.Date ? "今日邀请已送达，不再重复提醒。" :
            localNow.TimeOfDay.TotalMinutes >= Math.Min(1440, p.OfficeReminderMinute + 30) ? "今天的预约时间已过，明天再继续。" :
            $"预约时间 {ReminderCopy.Clock(p.OfficeReminderMinute)} · 由你点击开始";
        AppointmentDetail.Text = p.OfficeReminderEnabled ? "遵循工作与静默安排；当日最多等待 30 分钟，最迟到午夜。" : "点「调整安排」，为每天留一个固定的活动时间。";
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
                Breaks = day is null ? "—" : $"{day.MovementBreaks + day.OfficeBreaks} / {day.EyeBreaks}",
                Maintenance = day is null ? "—" : $"{day.MaintenanceSeconds.Values.Sum() / 60:0.#} 分钟" };
        }).ToArray();
    }
    private void Restore_Click(object sender, RoutedEventArgs e) => _app.RestoreBackup();
    private void Export_Click(object sender, RoutedEventArgs e) => _app.ExportCsv();
    private void Welcome_Click(object sender, RoutedEventArgs e) => _app.ShowWelcome();
    private void Settings_Click(object sender, RoutedEventArgs e) => _app.ShowSettings();
    private void Schedule_Click(object sender, RoutedEventArgs e) => _app.ShowScheduleSettings();
    private void Movement_Click(object sender, RoutedEventArgs e) => _app.StartBreak(BreakKind.Movement);
    private void Eyes_Click(object sender, RoutedEventArgs e) => _app.StartBreak(BreakKind.Eyes);
    private void Maintenance_Click(object sender, RoutedEventArgs e) => _app.ShowMaintenance();
    private void MaintenanceSettings_Click(object sender, RoutedEventArgs e) => _app.ShowMaintenanceSettings();
    private void ShortMaintenance_Click(object sender, RoutedEventArgs e) => _app.StartMaintenance(false);
    private void FullMaintenance_Click(object sender, RoutedEventArgs e) => _app.StartMaintenance(true);
    private void Office_Click(object sender, RoutedEventArgs e) => _app.StartBreak(BreakKind.Office);
    private void Pause_Click(object sender, RoutedEventArgs e) => _app.TogglePause();
}
