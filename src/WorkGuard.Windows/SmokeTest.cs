using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using WorkGuard.Data;
using WorkGuard.Windows.Platform;
using WorkGuard.Windows.Views;

namespace WorkGuard.Windows;

// Explicit CI mode: disposable data, no startup-registry changes or user screenshots.
internal static class SmokeTest
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static void Click(Window window, string name) =>
        ((Button)window.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Capture(Window window, string name)
    {
        // DataGrid star widths and tab templates settle on the dispatcher after Loaded.
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        window.UpdateLayout();
        var directory = Environment.GetEnvironmentVariable("WORKGUARD_CAPTURE_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        var content = (FrameworkElement)window.Content;
        var width = content.ActualWidth + content.Margin.Left + content.Margin.Right;
        var height = content.ActualHeight + content.Margin.Top + content.Margin.Bottom;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(height), 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(window.Background, null, new Rect(0, 0, width, height));
            drawing.DrawRectangle(new VisualBrush(content) { Stretch = Stretch.Fill }, null, new Rect(content.Margin.Left, content.Margin.Top, content.ActualWidth, content.ActualHeight));
        }
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, name + ".png")); encoder.Save(file);
    }

    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }

    public static void Run(string resultFile)
    {
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "WorkGuardSmoke-" + Guid.NewGuid());
            var windows = new List<Window>();
            try
            {
                using var app = new AppController(folder);
                Motion.Configure(true); // Stable screenshots; exercise real animation clocks separately below.
                if (WindowsActivity.IdleAge() is null) throw new InvalidOperationException("GetLastInputInfo failed");
                _ = WindowsActivity.IsOtherAppFullscreen();
                using var tray = new TrayIcon(app);
                // Deterministic fixture data; never written into the user's data directory.
                for (var i = 0; i < 7; i++)
                {
                    var day = LocalStore.Day(app.State, DateOnly.FromDateTime(DateTime.Now).AddDays(-i));
                    day.ActiveSeconds = 13080 + i * 120; day.LongestSeconds = 2820 + i * 60;
                    day.EyeBreaks = 8; day.MovementBreaks = 3; day.OfficeBreaks = 1;
                }
                for (var i = 0; i < 37 * 60; i++) app.Engine.Advance(TimeSpan.FromSeconds(1), new(TimeSpan.Zero, Quiet: true));
                var dashboard = new DashboardWindow(app); windows.Add(dashboard); dashboard.Show();
                Capture(dashboard, "01-today");
                Check(dashboard.HeroArt.Source is BitmapSource { PixelWidth: 1000 }, "Embedded illustration missing or unbounded decode");
                var symbol = new Symbol();
                Check(new Typeface(symbol.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal).TryGetGlyphTypeface(out var font), "Icon font missing");
                foreach (var code in new[] { 0xE713, 0xE768, 0xE890, 0xE916, 0xE769, 0xE72C, 0xE74E, 0xE711, 0xE8B7, 0xE893, 0xE823, 0xE80F, 0xE787, 0xE708, 0xE7F4, 0xE8FB })
                    Check(font.CharacterToGlyphMap.ContainsKey(code), $"Missing icon glyph {code:X}");
                dashboard.Sections.SelectedIndex = 1; Capture(dashboard, "02-history");
                Check(dashboard.WeekGrid.Columns.All(c => c.ActualWidth > 100), "Statistics columns collapsed");
                var settings = new SettingsWindow(app); windows.Add(settings); settings.Show();
                Capture(settings, "03-rhythm"); settings.Sections.SelectedIndex = 1; Capture(settings, "12-strict-settings");
                settings.Sections.SelectedIndex = 2; Capture(settings, "04-quiet");
                settings.Sections.SelectedIndex = 3; Capture(settings, "05-data");
                settings.EyeInterval.Text = "invalid"; Click(settings, "SaveButton");
                Check(settings.IsVisible && settings.ValidationText.Text.Length > 0, "Invalid setting accepted");
                var reminder = new ReminderWindow(BreakKind.Movement); windows.Add(reminder); reminder.Show();
                Check(!reminder.ShowActivated, "Reminder steals focus"); Capture(reminder, "06-reminder"); reminder.Close();
                var eyes = new BreakWindow(BreakKind.Eyes, true, false, TimeSpan.FromMinutes(20), false);
                windows.Add(eyes); eyes.Show(); Capture(eyes, "07-eyes-ready");
                var eyeCompleted = 0; eyes.Completed += _ => eyeCompleted++;
                Click(eyes, "StartButton"); for (var i = 0; i < 20; i++) eyes.Tick(TimeSpan.FromSeconds(1));
                Check(eyeCompleted == 1 && !eyes.IsVisible, "Eye completion failed");
                var body = new BreakWindow(BreakKind.Movement, true, false, TimeSpan.FromMinutes(60), false);
                windows.Add(body); body.Show(); Capture(body, "08-body-ready");
                var completed = 0; body.Completed += _ => completed++;
                Check(!body.HasStarted, "Body activity starts without consent");
                Click(body, "StartButton"); body.Tick(TimeSpan.FromSeconds(10)); Capture(body, "09-body-running");
                body.PauseForInterruption(); var count = body.Countdown.Text; body.Tick(TimeSpan.FromSeconds(5));
                Check(body.Countdown.Text == count && !body.IsRunning, "Paused body activity advances"); Capture(body, "10-body-paused");
                Click(body, "PauseButton"); for (var i = 0; i < 170; i++) body.Tick(TimeSpan.FromSeconds(1));
                Check(completed == 1 && body.IsFinished && !body.IsRunning, "Full body activity not credited once"); Capture(body, "11-body-complete"); body.Close();
                var skip = new BreakWindow(BreakKind.Movement, true, false, TimeSpan.Zero, false);
                windows.Add(skip); skip.Show(); var skippedCredit = 0; skip.Completed += _ => skippedCredit++;
                Click(skip, "StartButton"); for (var i = 0; i < 6; i++) Click(skip, "SkipButton");
                Check(skippedCredit == 0, "Skipped activity credited"); skip.Close();
                var forced = new BreakWindow(BreakKind.Movement, true, true, TimeSpan.FromMinutes(60), false, strict: true, neck: true);
                windows.Add(forced); var forcedCredit = 0; forced.Completed += _ => forcedCredit++; forced.Show();
                Check(forced.IsRunning && forced.ShowActivated, "Strict break did not start on show");
                Check(forced.StartButton.Visibility == Visibility.Collapsed && forced.SkipButton.Visibility == Visibility.Collapsed && forced.PauseButton.Visibility == Visibility.Collapsed, "Strict bypass controls exposed");
                var beforeSkip = forced.Countdown.Text; Click(forced, "SkipButton"); Click(forced, "PauseButton");
                Check(forced.Countdown.Text == beforeSkip && forced.IsRunning, "Strict skip/pause bypass");
                Capture(forced, "13-strict-movement");
                for (var i = 0; i < 60; i++) forced.Tick(TimeSpan.FromSeconds(1));
                Check(forced.Heading.Text.Contains("两侧"), "Neck scene missing"); Capture(forced, "14-strict-neck");
                forced.Close(); Check(forced.IsVisible && forced.EmergencyPanel.Visibility == Visibility.Visible, "Close bypasses strict exit confirmation");
                Capture(forced, "15-strict-emergency"); Click(forced, "ContinueButton");
                forced.PauseForInterruption(); beforeSkip = forced.Countdown.Text; forced.Tick(TimeSpan.FromSeconds(10));
                Check(!forced.IsRunning && beforeSkip == forced.Countdown.Text && forced.PauseButton.IsVisible, "Strict interruption did not require resume");
                Click(forced, "PauseButton"); for (var i = 0; i < 120; i++) forced.Tick(TimeSpan.FromSeconds(1));
                Check(forcedCredit == 1 && !forced.IsVisible, "Strict completion did not release window once");
                var rest = new BreakWindow(BreakKind.Movement, true, true, TimeSpan.Zero, false, strict: true);
                windows.Add(rest); rest.Show(); Click(rest, "AlternativeButton");
                Check(rest.Countdown.Text == "03:00", "Rest alternative shortened session"); Capture(rest, "16-strict-alternative");
                var restCredit = 0; rest.Completed += s => { Check(s.RestOnly && !s.ActivityCompleted, "Alternative credited activity"); restCredit++; };
                for (var i = 0; i < 180; i++) rest.Tick(TimeSpan.FromSeconds(1)); Check(restCredit == 1 && !rest.IsVisible, "Alternative did not finish");
                var strictEyes = new BreakWindow(BreakKind.Eyes, true, true, TimeSpan.Zero, false, strict: true, variant: 1);
                windows.Add(strictEyes); strictEyes.Show(); Capture(strictEyes, "17-strict-eyes");
                Check(strictEyes.ActualWidth > 600 && strictEyes.HasStarted, "Strict eyes not fullscreen");
                var earlyCredit = 0; strictEyes.Completed += _ => earlyCredit++; Click(strictEyes, "ExitButton"); Click(strictEyes, "ConfirmExitButton");
                Check(!strictEyes.IsVisible && earlyCredit == 0, "Emergency exit awarded completion");
                var cover = new RestCoverWindow(System.Windows.Forms.Screen.PrimaryScreen!, () => { });
                windows.Add(cover); cover.Show(); cover.Update("休息中", "还有 20 秒", Brushes.Beige); cover.Release();
                Check(!cover.IsVisible, "Companion cover failed to release");
                // Real WPF animation clock: intermediate frames and settled state, no simulation of session time.
                Motion.SystemAnimationOverride = true; Motion.Configure(false);
                var animated = new BreakWindow(BreakKind.Movement, true, false, TimeSpan.Zero, false);
                windows.Add(animated); animated.Show(); animated.BeginSession();
                Motion.Reveal(animated.SceneContent);
                if (Motion.Enabled)
                {
                    Check(animated.SceneContent.HasAnimatedProperties, "Entrance has no animation clock");
                    Capture(animated, "18-motion-start"); Pump(130); Capture(animated, "19-motion-middle");
                    Check(animated.SceneContent.Opacity > 0 && animated.SceneContent.Opacity < 1, "Entrance did not interpolate");
                    Pump(450); Capture(animated, "20-motion-settled");
                    Check(animated.SceneContent.Opacity == 1, "Entrance did not settle");
                    Motion.Reveal(animated.SceneContent); Motion.Configure(true);
                    Check(!animated.SceneContent.HasAnimatedProperties && animated.SceneContent.Opacity == 1, "Reduce motion left active entrance clock");
                }
                Motion.SystemAnimationOverride = false; Motion.Configure(false);
                Motion.Reveal(animated.SceneContent); Check(!animated.SceneContent.HasAnimatedProperties, "System reduced motion ignored");
                Motion.SystemAnimationOverride = null; Motion.Configure(true); animated.Close();
                dashboard.Width = 820; dashboard.Height = 620; Capture(dashboard, "21-compact-dashboard");
                var compact = new BreakWindow(BreakKind.Eyes, true, false, TimeSpan.Zero, false);
                windows.Add(compact); compact.Show(); compact.Width = 640; compact.Height = 620; Capture(compact, "22-compact-rest");
                Check(compact.ScenicPanel.Visibility == Visibility.Collapsed && compact.StartButton.IsVisible, "Compact rest hides primary action"); compact.Close();
                // Route and statistics integration, using the production controller without its wall-clock timer.
                app.State.Preferences = app.State.Preferences with { StrictMode = true };
                app.StartBreak(BreakKind.Movement, automatic: true, strict: true);
                var integrated = Application.Current.Windows.OfType<BreakWindow>().Single(w => w.IsVisible);
                windows.Add(integrated); var dayBefore = LocalStore.Day(app.State, DateOnly.FromDateTime(DateTime.Now));
                var bodyCount = dayBefore.MovementBreaks; var eyeCount = dayBefore.EyeBreaks;
                Click(integrated, "AlternativeButton"); for (var i = 0; i < 180; i++) integrated.Tick(TimeSpan.FromSeconds(1));
                Check(dayBefore.MovementBreaks == bodyCount && dayBefore.EyeBreaks == eyeCount + 1, "Controller credited fallback as movement");
                Check(app.Engine.Continuous > TimeSpan.Zero && app.Engine.SnoozeRemaining == TimeSpan.FromMinutes(5), "Fallback erased body exposure or failed to snooze");
                File.WriteAllText(resultFile, "PASS: WPF views, layout, settings validation, reminder focus policy, pause, completion, skip, tray, input API, foreground detection");
            }
            catch (Exception error)
            {
                File.WriteAllText(resultFile, "FAIL: " + error); Application.Current.Shutdown(1); return;
            }
            finally
            {
                Motion.SystemAnimationOverride = null; Motion.Configure(true);
                foreach (var window in windows) if (window.IsLoaded) { if (window is BreakWindow breakWindow) breakWindow.CloseForSystem(); else window.Close(); }
                if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            }
            Application.Current.Shutdown(0);
        }));
    }
}
