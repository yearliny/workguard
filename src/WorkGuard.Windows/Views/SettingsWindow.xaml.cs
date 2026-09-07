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
        EyeInterval.Text = p.EyeIntervalMinutes.ToString();
        MovementInterval.Text = p.MovementIntervalMinutes.ToString();
        NaturalRest.Text = p.NaturalRestMinutes.ToString();
        EyeEnabled.IsChecked = p.EyeReminders;
        InferRest.IsChecked = p.InferNaturalRest;
        QuietFullscreen.IsChecked = p.QuietWhenFullscreen;
        Startup.IsChecked = p.StartWithWindows;
        Gentle.IsChecked = p.GentleOnly;
        Sound.IsChecked = p.SoundEnabled;
        QuietEnabled.IsChecked = p.QuietHoursEnabled;
        QuietStart.Text = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(p.QuietStartMinute)).ToString("HH:mm");
        QuietEnd.Text = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(p.QuietEndMinute)).ToString("HH:mm");
        BackupButton.IsEnabled = app.CanRestoreBackup;
        SaveButton.Content = p.OnboardingComplete ? "保存设置" : "保存并开始";
        VersionText.Text = "WorkGuard " + typeof(App).Assembly.GetName().Version?.ToString(3);
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
        var p = _app.State.Preferences with
        {
            EyeIntervalMinutes = eye, MovementIntervalMinutes = movement, NaturalRestMinutes = rest,
            EyeReminders = EyeEnabled.IsChecked == true, InferNaturalRest = InferRest.IsChecked == true,
            QuietWhenFullscreen = QuietFullscreen.IsChecked == true, StartWithWindows = Startup.IsChecked == true,
            GentleOnly = Gentle.IsChecked == true, SoundEnabled = Sound.IsChecked == true,
            QuietHoursEnabled = QuietEnabled.IsChecked == true, QuietStartMinute = start.Hour * 60 + start.Minute,
            QuietEndMinute = end.Hour * 60 + end.Minute
        };
        if (_app.ApplyPreferences(p)) Close();
        else ValidationText.Text = _app.DataError;
    }
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
