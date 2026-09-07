using System.Windows.Threading;
using WorkGuard.Windows.Platform;
using WorkGuard.Windows.Views;

namespace WorkGuard.Windows;

// CI-only opt-in mode. It uses a disposable data directory and never changes startup settings.
internal static class SmokeTest
{
    public static void Run(string resultFile)
    {
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "WorkGuardSmoke-" + Guid.NewGuid());
            var windows = new List<Window>();
            try
            {
                using var app = new AppController(folder);
                if (WindowsActivity.IdleAge() is null) throw new InvalidOperationException("GetLastInputInfo failed");
                _ = WindowsActivity.IsOtherAppFullscreen();
                using var tray = new TrayIcon(app);
                windows.Add(new DashboardWindow(app));
                windows.Add(new SettingsWindow(app));
                windows.Add(new BreakWindow(BreakKind.Eyes, true, false, TimeSpan.FromMinutes(20)));
                windows.Add(new BreakWindow(BreakKind.Movement, true, false, TimeSpan.FromMinutes(60)));
                foreach (var window in windows) { window.Show(); window.UpdateLayout(); }
                File.WriteAllText(resultFile, "PASS: WPF resources, four windows, tray, input API, foreground detection");
            }
            catch (Exception error)
            {
                File.WriteAllText(resultFile, "FAIL: " + error);
                Application.Current.Shutdown(1);
                return;
            }
            finally
            {
                foreach (var window in windows) window.Close();
                if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            }
            Application.Current.Shutdown(0);
        }));
    }
}
