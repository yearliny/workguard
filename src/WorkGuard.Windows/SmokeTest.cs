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

    private static T FindAncestor<T>(DependencyObject element) where T : DependencyObject
    {
        for (var parent = VisualTreeHelper.GetParent(element); parent is not null; parent = VisualTreeHelper.GetParent(parent))
            if (parent is T result) return result;
        throw new InvalidOperationException("Expected visual ancestor " + typeof(T).Name);
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
                app.State.Preferences = app.State.Preferences with { OnboardingComplete = true };
                for (var i = 0; i < 37 * 60; i++) app.Engine.Advance(TimeSpan.FromSeconds(1), new(TimeSpan.Zero, Quiet: true));
                var dashboard = new DashboardWindow(app); windows.Add(dashboard); dashboard.Show();
                Capture(dashboard, "01-today");
                var actionViewport = FindAncestor<ScrollViewer>(dashboard.EyesAction);
                var actionBounds = dashboard.EyesAction.TransformToAncestor(actionViewport).TransformBounds(new Rect(dashboard.EyesAction.RenderSize));
                Check(actionBounds.Bottom <= actionViewport.ActualHeight + 1, "Default dashboard clips activity actions");
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
                Check(!body.HasStarted && body.RouteItems.Items.Count == 3, "Body preview missing or starts without consent");
                Click(body, "StartButton"); body.Tick(TimeSpan.FromSeconds(10)); Capture(body, "09-body-running");
                body.PauseForInterruption(); var count = body.Countdown.Text; body.Tick(TimeSpan.FromSeconds(5));
                Check(body.Countdown.Text == count && !body.IsRunning, "Paused body activity advances"); Capture(body, "10-body-paused");
                Click(body, "PauseButton"); for (var i = 0; i < 170; i++) body.Tick(TimeSpan.FromSeconds(1));
                Check(completed == 1 && body.IsFinished && !body.IsRunning, "Full body activity not credited once"); Capture(body, "11-body-complete"); body.Close();
                var journey = new BreakWindow(BreakKind.Office, true, false, TimeSpan.Zero, false);
                windows.Add(journey); journey.Show(); journey.BeginSession();
                Check(journey.TotalCountdown.Text == "总剩余 07:00", "Total countdown missing at start");
                var movementArt = journey.RestArt.Source;
                for (var i = 0; i < 12; i++) journey.Tick(TimeSpan.FromSeconds(10));
                Check(journey.TotalCountdown.Text == "总剩余 05:00" && journey.Countdown.Text == "01:00", "Total resets on step transition");
                Check(Math.Abs(journey.TotalProgress.Value - 200d / 7) < .001, "Total progress does not span session");
                Check(!ReferenceEquals(movementArt, journey.RestArt.Source), "Scenes reuse identical illustration");
                Capture(journey, "29-office-total-progress");
                journey.PauseForInterruption(); journey.Tick(TimeSpan.FromSeconds(10));
                Check(journey.TotalCountdown.Text == "总剩余 05:00" && journey.JourneyNote.Text.Contains("已暂停"), "Total advances while paused");
                journey.Close();
                var skip = new BreakWindow(BreakKind.Movement, true, false, TimeSpan.Zero, false);
                windows.Add(skip); skip.Show(); var skippedCredit = 0; skip.Completed += _ => skippedCredit++;
                Click(skip, "StartButton"); for (var i = 0; i < 6; i++) Click(skip, "SkipButton");
                Check(skippedCredit == 0 && skip.SessionRing.Value == 0 && skip.CompletionSymbol.Text != "\uE8FB", "Skipped activity displayed as complete"); Capture(skip, "23-incomplete"); skip.Close();
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
                Check(rest.RoutePanel.Visibility == Visibility.Collapsed, "Alternative still prescribes movement");
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
                    Capture(animated, "18-motion-start");
                    // PNG encoding can outlast the 320 ms entrance on CI. Start a fresh
                    // production animation and sample before doing any screenshot I/O.
                    Motion.Reveal(animated.SceneContent); Pump(130);
                    Check(animated.SceneContent.Opacity > 0 && animated.SceneContent.Opacity < 1, "Entrance did not interpolate");
                    Capture(animated, "19-motion-middle");
                    Pump(450); Capture(animated, "20-motion-settled");
                    Check(animated.SceneContent.Opacity == 1, "Entrance did not settle");
                    Motion.Reveal(animated.SceneContent); Motion.Configure(true);
                    Check(!animated.SceneContent.HasAnimatedProperties && animated.SceneContent.Opacity == 1, "Reduce motion left active entrance clock");
                }
                Motion.SystemAnimationOverride = false; Motion.Configure(false);
                Motion.Reveal(animated.SceneContent); Check(!animated.SceneContent.HasAnimatedProperties, "System reduced motion ignored");
                Motion.SystemAnimationOverride = null; Motion.Configure(true); animated.Close();
                dashboard.Sections.SelectedIndex = 0;
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
                Check(app.LastCompletion?.Contains("未计为身体活动") == true, "Fallback feedback claims body completion");
                var saveCalls = 0; var finishedCalls = 0; var allowSave = false; Preferences? saved = null;
                var welcome = new WelcomeWindow(new Preferences(), p => { saveCalls++; if (!allowSave) return false; saved = p; return true; }, () => "测试：保存失败，请重试。", () => finishedCalls++);
                windows.Add(welcome); welcome.Show(); Capture(welcome, "24-welcome-rhythm");
                Check(welcome.GentleMode.IsChecked == true && saveCalls == 0, "Welcome silently opts into strict or saves early");
                welcome.DailyRhythm.IsChecked = true; Click(welcome, "NextButton");
                welcome.StrictMode.IsChecked = true; welcome.SoundEnabled.IsChecked = false;
                Capture(welcome, "25-welcome-mode"); Click(welcome, "BackButton");
                Check(welcome.DailyRhythm.IsChecked == true && saveCalls == 0, "Back lost selection or saved");
                Click(welcome, "NextButton"); Click(welcome, "NextButton"); Capture(welcome, "26-welcome-ready");
                Click(welcome, "NextButton"); Check(welcome.IsVisible && saveCalls == 1 && finishedCalls == 0 && welcome.ErrorText.Text.Length > 0, "Failed onboarding save closed window");
                welcome.Width = 680; welcome.Height = 580; Capture(welcome, "27-welcome-compact-error");
                Check(welcome.ArtPanel.Visibility == Visibility.Collapsed && welcome.NextButton.IsVisible, "Compact onboarding hides save");
                allowSave = true; Click(welcome, "NextButton");
                Check(!welcome.IsVisible && finishedCalls == 1 && saved is { MovementIntervalMinutes: 50, StrictMode: true, SoundEnabled: false, OnboardingComplete: true }, "Onboarding retry failed");
                var abandoned = new WelcomeWindow(new Preferences(), _ => throw new InvalidOperationException("Close saved onboarding"), () => null, () => throw new InvalidOperationException("Close finished onboarding"));
                windows.Add(abandoned); abandoned.Show(); abandoned.Close();
                // Calendar and delivery use the real controller, with deterministic injected samples.
                using var scheduled = new AppController(Path.Combine(folder, "schedule"));
                scheduled.State.Preferences = scheduled.State.Preferences with
                { OnboardingComplete = true, OfficeReminderEnabled = true, StrictMode = true, ReduceMotion = true };
                scheduled.Engine.Configure(scheduled.State.Preferences);
                var noon = new DateTimeOffset(DateTime.Today.AddHours(15));
                scheduled.MeetingMode = true;
                for (var i = 0; i < 5; i++) scheduled.Advance(TimeSpan.FromSeconds(1), noon.AddSeconds(i), TimeSpan.Zero, false);
                Check(!Application.Current.Windows.OfType<ReminderWindow>().Any(w => w.IsVisible), "Appointment interrupted meeting");
                Check(scheduled.Status.Contains("会议"), "Meeting status is misleading");
                scheduled.MeetingMode = false;
                scheduled.Advance(TimeSpan.FromSeconds(1), noon.AddSeconds(5), TimeSpan.Zero, false);
                Check(scheduled.Delivery.Reason == DeliveryReason.Returning && scheduled.State.LastOfficeReminderDate is null, "Meeting exit skipped return buffer");
                for (var i = 0; i < 15; i++) scheduled.Advance(TimeSpan.FromSeconds(1), noon.AddSeconds(6 + i), TimeSpan.Zero, false);
                var invitation = Application.Current.Windows.OfType<ReminderWindow>().Single(w => w.IsVisible);
                windows.Add(invitation); Capture(invitation, "30-office-invitation");
                Check(invitation.Kind == BreakKind.Office && !invitation.ShowActivated, "Appointment type/focus incorrect");
                Check(invitation.StartButton.Content.ToString()!.Contains("7"), "Office invitation labels wrong duration");
                Check(scheduled.State.LastOfficeReminderDate == DateOnly.FromDateTime(noon.DateTime), "Invitation was not persisted");
                Check(!scheduled.State.Days.Any(d => d.OfficeBreaks > 0), "Invitation awarded activity credit");
                Click(invitation, "StartButton");
                var office = Application.Current.Windows.OfType<BreakWindow>().Single(w => w.IsVisible);
                windows.Add(office);
                Check(office.IsRunning && office.SkipButton.Visibility != Visibility.Collapsed, "Appointment became forced training");
                office.CloseForSystem();
                using (var restarted = new AppController(Path.Combine(folder, "schedule")))
                {
                    restarted.Advance(TimeSpan.FromSeconds(1), noon.AddMinutes(1), TimeSpan.Zero, false);
                    Check(!Application.Current.Windows.OfType<ReminderWindow>().Any(w => w.IsVisible), "Restart duplicated daily invitation");
                }
                var calendar = new DashboardWindow(scheduled); windows.Add(calendar); calendar.Show();
                scheduled.State.Preferences = scheduled.State.Preferences with { WorkScheduleEnabled = true, WorkDays = 62, QuietHoursEnabled = true };
                scheduled.Advance(TimeSpan.FromSeconds(1), noon.AddHours(5), TimeSpan.Zero, false);
                calendar.Sections.SelectedItem = calendar.ScheduleTab; Capture(calendar, "28-daily-plan");
                Check(calendar.DeliveryTitle.Text.Contains("工作时段之外"), "Calendar status contradicts delivery policy");
                calendar.Width = 820; calendar.Height = 620; Capture(calendar, "31-compact-daily-plan");
                var plan = new SettingsWindow(scheduled); windows.Add(plan); plan.Show();
                plan.Sections.SelectedItem = plan.ScheduleTab; Capture(plan, "29-schedule-settings");
                plan.WorkEnabled.IsChecked = true;
                foreach (var day in new[] { plan.Monday, plan.Tuesday, plan.Wednesday, plan.Thursday, plan.Friday, plan.Saturday, plan.Sunday }) day.IsChecked = false;
                Click(plan, "SaveButton");
                Check(plan.IsVisible && plan.ValidationText.Text.Contains("至少一个"), "Empty workdays accepted");
                plan.Friday.IsChecked = true; plan.WorkStart.Text = "22:00"; plan.WorkEnd.Text = "07:00";
                plan.OfficeTime.Text = "25:00"; Click(plan, "SaveButton");
                Check(plan.IsVisible && plan.ValidationText.Text.Contains("预约"), "Invalid appointment accepted");
                plan.OfficeTime.Text = "23:00";
                plan.Width = 700; plan.Height = 580; Capture(plan, "32-compact-schedule-settings");
                var inputViewport = FindAncestor<ScrollViewer>(plan.OfficeTime);
                var inputBounds = plan.OfficeTime.TransformToAncestor(inputViewport).TransformBounds(new Rect(plan.OfficeTime.RenderSize));
                Check(inputBounds.Top >= -1 && inputBounds.Bottom <= inputViewport.ActualHeight + 1, "Focused schedule input clipped after resizing");
                Click(plan, "SaveButton");
                Check(!plan.IsVisible && scheduled.State.Preferences is { WorkDays: 32, WorkStartMinute: 1320, WorkEndMinute: 420, OfficeReminderMinute: 1380 }, "Valid overnight schedule failed to save");
                using (var reopened = new AppController(Path.Combine(folder, "schedule")))
                    Check(reopened.State.LastOfficeReminderDate == DateOnly.FromDateTime(noon.DateTime), "Saving preferences lost daily receipt");
                // Write failure must not claim that an invitation was delivered.
                using var unwritable = new AppController(Path.Combine(folder, "blocked"));
                unwritable.State.Preferences = new Preferences { OnboardingComplete = true, OfficeReminderEnabled = true };
                File.WriteAllText(Path.Combine(folder, "blocked"), "file occupies data directory");
                unwritable.Advance(TimeSpan.FromSeconds(1), noon, TimeSpan.Zero, false);
                Check(unwritable.DataError is not null && unwritable.State.LastOfficeReminderDate is null, "Failed save falsely marked invitation delivered");
                Check(!Application.Current.Windows.OfType<ReminderWindow>().Any(w => w.IsVisible), "Invitation displayed before durable reservation");
                VerifyMaintenance(folder, windows);
                File.WriteAllText(resultFile, "PASS: WPF views, layout, settings validation, reminder focus policy, pause, completion, skip, tray, input API, foreground detection");
            }
            catch (Exception error)
            {
                File.WriteAllText(resultFile, "FAIL: " + error); Application.Current.Shutdown(1); return;
            }
            finally
            {
                Motion.SystemAnimationOverride = null; Motion.Configure(true);
                foreach (var window in windows) if (window.IsLoaded) { if (window is BreakWindow breakWindow) breakWindow.CloseForSystem(); else if (window is MaintenanceWindow maintenanceWindow) maintenanceWindow.CloseForSystem(); else window.Close(); }
                if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            }
            Application.Current.Shutdown(0);
        }));
    }
    private static void VerifyMaintenance(string folder, List<Window> windows)
    {
        using var app = new AppController(Path.Combine(folder, "maintenance"));
        app.State.Preferences = new Preferences { OnboardingComplete = true, MaintenanceEnabled = true, MaintenanceVoice = false };
        app.Engine.Configure(app.State.Preferences);
        var now = new DateTimeOffset(DateTime.Today.AddHours(9));
        app.Advance(TimeSpan.FromSeconds(1), now, TimeSpan.Zero, false);
        var dashboard = new DashboardWindow(app); windows.Add(dashboard); dashboard.Show();
        dashboard.Sections.SelectedItem = dashboard.MaintenanceTab; Capture(dashboard, "30-maintenance-today");
        var settings = new SettingsWindow(app); windows.Add(settings); settings.Show();
        settings.Sections.SelectedItem = settings.MaintenanceTab; Capture(settings, "31-maintenance-preferences"); settings.Close();
        app.StartMaintenance(false);
        var window = Application.Current.Windows.OfType<MaintenanceWindow>().Single(); windows.Add(window);
        Check(!window.HasStarted && window.Countdown.Text == "02:10", "Maintenance estimate excludes preparation");
        Click(window, "StartButton");
        for (var i = 0; i < 12; i++) app.Advance(TimeSpan.FromSeconds(1), now = now.AddSeconds(1), TimeSpan.Zero, false);
        Capture(window, "32-maintenance-guidance");
        Check(window.Session.ObservedPractice == 7, "Preparation counted as practice");
        app.Advance(TimeSpan.FromSeconds(1), now = now.AddSeconds(1), TimeSpan.Zero, false, unavailable: true);
        var before = window.Session.ObservedPractice;
        app.Advance(TimeSpan.FromSeconds(10), now = now.AddSeconds(10), TimeSpan.Zero, false);
        Check(window.Session.Paused && window.Session.ObservedPractice == before, "Unlock auto-resumes maintenance");
        Click(window, "PauseButton");
        app.Advance(TimeSpan.FromSeconds(60), now = now.AddSeconds(60), TimeSpan.Zero, false);
        Check(window.Session.Paused && window.Session.ObservedPractice == before, "Long gap credited maintenance");
        Click(window, "PauseButton");
        for (var i = 0; i < 118; i++) app.Advance(TimeSpan.FromSeconds(1), now = now.AddSeconds(1), TimeSpan.Zero, false);
        Check(window.Session.FullyPracticed && !window.Topmost && window.WindowStyle == WindowStyle.SingleBorderWindow,
            "Completion did not release fullscreen before confirmation");
        Check(app.State.Days.Sum(d => d.MaintenanceSeconds.Values.Sum()) == 0, "Playback granted maintenance credit");
        Capture(window, "33-maintenance-confirmation"); Click(window, "ConfirmButton");
        Check(app.State.Days.Sum(d => d.MaintenanceSeconds.Values.Sum()) == 120 && !window.IsVisible, "Confirmed maintenance not credited exactly once");
        Check(app.State.Days.Sum(d => d.OfficeBreaks + d.MovementBreaks) == 0, "Maintenance double-counted generic activity");
        Check(app.MaintenancePlan(true).Sum(s => s.Seconds) == 360, "Concentrated maintenance did not resume today's plan");
        app.StartMaintenance(false, strict: true);
        window = Application.Current.Windows.OfType<MaintenanceWindow>().Single(); windows.Add(window);
        var blocked = window.Session.Current.Exercise.Id;
        Check(window.HasStarted && window.DiscomfortButton.IsVisible && window.PauseButton.Visibility == Visibility.Collapsed, "Strict maintenance safety controls missing");
        Click(window, "DiscomfortButton");
        Check(!window.IsVisible && app.State.Preferences.MaintenanceBlockedExercises.Contains(blocked), "Discomfort failed to exit and block action");
        Check(app.MaintenancePlan(true).All(s => s.Exercise.Id != blocked), "Blocked action returned in plan");
        using (var reopened = new AppController(Path.Combine(folder, "maintenance")))
            Check(reopened.State.Preferences.MaintenanceBlockedExercises.Contains(blocked) && reopened.State.Days.Sum(d => d.MaintenanceSeconds.Values.Sum()) == 120, "Maintenance state lost on restart");
        Capture(dashboard, "34-maintenance-progress"); dashboard.Close();
        var blockedPath = Path.Combine(folder, "maintenance-write-failure");
        File.WriteAllText(blockedPath, "occupied");
        using var failure = new AppController(blockedPath);
        failure.State.Preferences = new Preferences { OnboardingComplete = true, MaintenanceVoice = false };
        failure.StartMaintenance(false, beginImmediately: true);
        var failingWindow = Application.Current.Windows.OfType<MaintenanceWindow>().Single(); windows.Add(failingWindow);
        for (var i = 0; i < 130; i++) failure.Advance(TimeSpan.FromSeconds(1), now = now.AddSeconds(1), TimeSpan.Zero, false);
        Click(failingWindow, "ConfirmButton");
        Check(failure.DataError is not null && failure.State.Days.Sum(d => d.MaintenanceSeconds.Values.Sum()) == 120, "Failed save lost in-memory confirmed maintenance");
        File.Delete(blockedPath); Check(failure.Save(), "Maintenance save did not recover");
        using var recovered = new AppController(blockedPath);
        Check(recovered.State.Days.Sum(d => d.MaintenanceSeconds.Values.Sum()) == 120, "Retry duplicated or lost maintenance credit");
    }

}
