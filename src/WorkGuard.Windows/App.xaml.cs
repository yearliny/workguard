using System.Threading;

namespace WorkGuard.Windows;

public partial class App : Application
{
    private Mutex? _mutex;
    private AppController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 2 && e.Args[0] == "--smoke-test")
        {
            SmokeTest.Run(e.Args[1]);
            return;
        }
        _mutex = new Mutex(true, @"Local\Yearliny.WorkGuard", out var first);
        if (!first)
        {
            MessageBox.Show("工作防沉迷已在运行，请从系统托盘打开。", "工作防沉迷");
            Shutdown();
            return;
        }
        _controller = new AppController();
        _controller.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
