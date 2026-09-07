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
        var p = _app.State.Preferences with
        {
            EyeIntervalMinutes = eye, MovementIntervalMinutes = movement, NaturalRestMinutes = rest,
            EyeReminders = EyeEnabled.IsChecked == true, InferNaturalRest = InferRest.IsChecked == true,
            QuietWhenFullscreen = QuietFullscreen.IsChecked == true, StartWithWindows = Startup.IsChecked == true,
            GentleOnly = Gentle.IsChecked == true
        };
        if (_app.ApplyPreferences(p)) Close();
    }
    private void Data_Click(object sender, RoutedEventArgs e)
    {
        try { WindowsActivity.OpenDataFolder(_app.DataDirectory); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        { ValidationText.Text = "无法打开数据文件夹：" + error.Message; }
    }
}
