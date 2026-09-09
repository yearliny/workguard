using System.Threading;
using WorkGuard.Windows.Platform;

namespace WorkGuard.Windows;

public partial class App : Application
{
    private Mutex? _mutex;
    private EventWaitHandle? _activate;
    private RegisteredWaitHandle? _activationWait;
    private FileStream? _dataLock;
    private AppController? _controller;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 2 && e.Args[0] == "--smoke-test")
        { SmokeTest.Run(e.Args[1]); return; }
        DispatcherUnhandledException += (_, error) =>
        {
            Diagnostics.Record(error.Exception);
            error.Handled = true;
            MessageBox.Show("程序遇到了意外错误，即将退出。已保存的数据会保留；本地诊断日志可帮助排查。", "工作防沉迷");
            _controller?.EndBreakForSystem();
            Shutdown(1);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, error) =>
        { if (error.ExceptionObject is Exception exception) Diagnostics.Record(exception); };
        _activate = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Yearliny.WorkGuard.Activate");
        _mutex = new Mutex(true, @"Local\Yearliny.WorkGuard", out var first);
        if (!first) { _activate.Set(); Shutdown(); return; }
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorkGuard");
            Directory.CreateDirectory(folder);
            _dataLock = new FileStream(Path.Combine(folder, "instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            _controller = new AppController(folder);
            try
            {
                // Keep the per-user Run entry aligned with the current installed/portable location.
                // This repairs upgrades or folder moves without creating duplicate startup entries.
                WindowsActivity.ReconcileStartup(_controller.State.Preferences.StartWithWindows);
            }
            catch (Exception startupError) when (startupError is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                // Startup registration is useful but must never prevent the app itself from running.
                Diagnostics.Record(startupError);
            }
            _activationWait = ThreadPool.RegisterWaitForSingleObject(_activate, (_, _) =>
            {
                if (!_exiting) Dispatcher.BeginInvoke(new Action(() => { if (!_exiting) _controller?.ShowDashboard(); }));
            }, null, Timeout.Infinite, false);
            SessionEnding += (_, _) => { _controller?.EndBreakForSystem(); _controller?.Save(); };
            _controller.Start();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Diagnostics.Record(error);
            MessageBox.Show("无法打开本地数据目录。请确认目录可写，且其他 Windows 会话没有运行工作防沉迷。", "工作防沉迷");
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _exiting = true;
        _activationWait?.Unregister(null);
        _controller?.Dispose();
        _dataLock?.Dispose();
        _activate?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
