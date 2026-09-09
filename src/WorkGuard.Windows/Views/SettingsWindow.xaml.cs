using WorkGuard.Windows.Platform;

namespace WorkGuard.Windows.Views;

public partial class SettingsWindow : Window
{
    private readonly AppController _app;
    internal SettingsWindow(AppController app)
    {
        InitializeComponent();
        _app = app;
        var p = app.State.Preferences;
        ValidationText.Text = app.DataError;
        EyeInterval.Text = p.EyeIntervalMinutes.ToString();
        MovementInterval.Text = p.MovementIntervalMinutes.ToString();
        NaturalRest.Text = p.NaturalRestMinutes.ToString();
        Strict.IsChecked = p.StrictMode;
        ForceEyes.IsChecked = p.StrictEyes;
        Neck.IsChecked = p.NeckMovements;
        MaintenanceEnabled.IsChecked = p.MaintenanceEnabled;
        MaintenanceGoal.Text = p.MaintenanceGoalMinutes.ToString();
        MaintenanceStanding.IsChecked = p.MaintenanceStanding;
        MaintenanceVoice.IsChecked = p.MaintenanceVoice;
        foreach (var area in new[] { BodyArea.Thoracic, BodyArea.Shoulders, BodyArea.Hips, BodyArea.BackLegs, BodyArea.Ankles, BodyArea.Neck })
            MaintenanceAreaChoices.Children.Add(new CheckBox { Tag = area, Content = MaintenanceCatalog.Label(area),
                IsChecked = (p.MaintenanceExcludedAreas & area) == 0, Margin = new Thickness(0, 0, 16, 12) });
        foreach (var id in p.MaintenanceBlockedExercises)
            BlockedExercises.Children.Add(new CheckBox { Tag = id, IsChecked = true,
                Content = MaintenanceCatalog.All.First(e => e.Id == id).Title, Margin = new Thickness(0, 5, 0, 5) });
        BlockedHint.Text = p.MaintenanceBlockedExercises.Length == 0 ? "暂无暂停的动作。" : "勾选表示继续暂停。确认适合恢复后，取消对应勾选并保存。";
        EyeEnabled.IsChecked = p.EyeReminders;
        InferRest.IsChecked = p.InferNaturalRest;
        QuietFullscreen.IsChecked = p.QuietWhenFullscreen;
        Startup.IsChecked = p.StartWithWindows;
        Gentle.IsChecked = p.GentleOnly;
        Sound.IsChecked = p.SoundEnabled;
        ReducedMotion.IsChecked = p.ReduceMotion;
        QuietEnabled.IsChecked = p.QuietHoursEnabled;
        QuietStart.Text = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(p.QuietStartMinute)).ToString("HH:mm");
        QuietEnd.Text = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(p.QuietEndMinute)).ToString("HH:mm");
        WorkEnabled.IsChecked = p.WorkScheduleEnabled;
        WorkStart.Text = ReminderCopy.Clock(p.WorkStartMinute);
        WorkEnd.Text = ReminderCopy.Clock(p.WorkEndMinute);
        OfficeEnabled.IsChecked = p.OfficeReminderEnabled;
        OfficeTime.Text = ReminderCopy.Clock(p.OfficeReminderMinute);
        for (var i = 0; i < 7; i++) DayChoices[i].IsChecked = WorkSchedule.IncludesDay(p, (DayOfWeek)i);
        BackupButton.IsEnabled = app.CanRestoreBackup;
        SaveButton.Content = p.OnboardingComplete ? "保存设置" : "保存并开始";
        VersionText.Text = "WorkGuard " + typeof(App).Assembly.GetName().Version?.ToString(3);
        SizeChanged += (_, _) => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() =>
        {
            if (System.Windows.Input.Keyboard.FocusedElement is FrameworkElement focused && IsAncestorOf(focused)) focused.BringIntoView();
        }));
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(EyeInterval.Text, out var eye) || eye is < 10 or > 60 ||
            !int.TryParse(MovementInterval.Text, out var movement) || movement is < 30 or > 120 ||
            !int.TryParse(NaturalRest.Text, out var rest) || rest is < 3 or > 15)
        {
            ValidationText.Text = "请输入范围内的整数：眼睛 10–60、身体 30–120、自然休息 3–15 分钟。";
            return;
        }
        if (!TimeOnly.TryParseExact(QuietStart.Text, "HH:mm", out var start) || !TimeOnly.TryParseExact(QuietEnd.Text, "HH:mm", out var end) ||
            (QuietEnabled.IsChecked == true && start == end))
        { ValidationText.Text = "请填写有效且不同的开始与结束时间，例如 12:00 与 13:00。"; return; }
        var days = Enumerable.Range(0, 7).Where(i => DayChoices[i].IsChecked == true).Sum(i => 1 << i);
        if (!TimeOnly.TryParseExact(WorkStart.Text, "HH:mm", out var workStart) ||
            !TimeOnly.TryParseExact(WorkEnd.Text, "HH:mm", out var workEnd) ||
            (WorkEnabled.IsChecked == true && (days == 0 || workStart == workEnd)))
        {
            Sections.SelectedItem = ScheduleTab;
            ValidationText.Text = "请选择至少一个工作日，并填写不同的起止时间（HH:mm）。";
            return;
        }
        if (!TimeOnly.TryParseExact(OfficeTime.Text, "HH:mm", out var officeTime))
        {
            Sections.SelectedItem = ScheduleTab;
            ValidationText.Text = "请填写有效的预约时间，例如 15:00。";
            OfficeTime.Focus();
            return;
        }
        if (!int.TryParse(MaintenanceGoal.Text, out var maintenanceGoal) || maintenanceGoal is < 5 or > 10)
        {
            Sections.SelectedItem = MaintenanceTab;
            ValidationText.Text = "每日跟练目标请填写 5–10 之间的整数。";
            MaintenanceGoal.Focus(); return;
        }
        var excluded = MaintenanceAreaChoices.Children.OfType<CheckBox>().Where(c => c.IsChecked != true)
            .Aggregate(BodyArea.None, (areas, c) => areas | (BodyArea)c.Tag);
        var blocked = BlockedExercises.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (string)c.Tag).ToArray();
        var p = _app.State.Preferences with
        {
            MaintenanceEnabled = MaintenanceEnabled.IsChecked == true, MaintenanceGoalMinutes = maintenanceGoal,
            MaintenanceStanding = MaintenanceStanding.IsChecked == true, MaintenanceVoice = MaintenanceVoice.IsChecked == true,
            MaintenanceExcludedAreas = excluded, MaintenanceBlockedExercises = blocked,
            StrictMode = Strict.IsChecked == true, StrictEyes = ForceEyes.IsChecked == true, NeckMovements = Neck.IsChecked == true,
            EyeIntervalMinutes = eye, MovementIntervalMinutes = movement, NaturalRestMinutes = rest,
            EyeReminders = EyeEnabled.IsChecked == true, InferNaturalRest = InferRest.IsChecked == true,
            QuietWhenFullscreen = QuietFullscreen.IsChecked == true, StartWithWindows = Startup.IsChecked == true,
            ReduceMotion = ReducedMotion.IsChecked == true,
            GentleOnly = Gentle.IsChecked == true, SoundEnabled = Sound.IsChecked == true,
            QuietHoursEnabled = QuietEnabled.IsChecked == true, QuietStartMinute = start.Hour * 60 + start.Minute,
            QuietEndMinute = end.Hour * 60 + end.Minute,
            WorkScheduleEnabled = WorkEnabled.IsChecked == true, WorkDays = days,
            WorkStartMinute = workStart.Hour * 60 + workStart.Minute, WorkEndMinute = workEnd.Hour * 60 + workEnd.Minute,
            OfficeReminderEnabled = OfficeEnabled.IsChecked == true, OfficeReminderMinute = officeTime.Hour * 60 + officeTime.Minute
        };
        if (_app.ApplyPreferences(p)) Close();
        else ValidationText.Text = _app.DataError;
    }
    private System.Windows.Controls.CheckBox[] DayChoices => [Sunday, Monday, Tuesday, Wednesday, Thursday, Friday, Saturday];
    private void PreviewStrict_Click(object sender, RoutedEventArgs e) => _app.StartBreak(BreakKind.Eyes, strict: true);
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    private void Preset_Click(object sender, RoutedEventArgs e) { EyeInterval.Text = "20"; MovementInterval.Text = ((System.Windows.Controls.Button)sender).Tag.ToString(); NaturalRest.Text = "5"; }
    private void Export_Click(object sender, RoutedEventArgs e) => _app.ExportCsv();
    private void Restore_Click(object sender, RoutedEventArgs e) => _app.RestoreBackup();
    private void Data_Click(object sender, RoutedEventArgs e)
    {
        try { WindowsActivity.OpenDataFolder(_app.DataDirectory); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        { ValidationText.Text = "无法打开数据文件夹：" + error.Message; }
    }
}
