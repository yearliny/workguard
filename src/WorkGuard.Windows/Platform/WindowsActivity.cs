using System.Diagnostics;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;
using Microsoft.Win32;

namespace WorkGuard.Windows.Platform;

internal static class WindowsActivity
{
    private const string StartupValueName = "WorkGuard";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInput { public uint Size; public uint Tick; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInput info);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr handle, out Rect rect);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint process);
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();

    public static TimeSpan? IdleAge()
    {
        var input = new LastInput { Size = (uint)Marshal.SizeOf<LastInput>() };
        if (!GetLastInputInfo(ref input)) return null;
        return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - input.Tick));
    }

    public static bool IsOtherAppFullscreen()
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero || handle == GetShellWindow() || handle == GetDesktopWindow()) return false;
        GetWindowThreadProcessId(handle, out var pid);
        if (pid == (uint)Environment.ProcessId || !GetWindowRect(handle, out var r)) return false;
        var b = Forms.Screen.FromHandle(handle).Bounds;
        return Math.Abs(r.Left - b.Left) <= 2 && Math.Abs(r.Top - b.Top) <= 2 &&
               Math.Abs(r.Right - b.Right) <= 2 && Math.Abs(r.Bottom - b.Bottom) <= 2;
    }

    public static string? StartupCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(StartupValueName) as string;
    }

    public static void RestoreStartup(string? command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (command is null) key.DeleteValue(StartupValueName, false);
        else key.SetValue(StartupValueName, command, RegistryValueKind.String);
    }

    public static void ReconcileStartup(bool enabled)
    {
        var current = StartupCommand();
        if (!enabled)
        {
            if (current is not null) SetStartup(false);
            return;
        }

        var desired = StartupCommandForCurrentExecutable();
        if (!string.Equals(current, desired, StringComparison.Ordinal)) SetStartup(true);
    }

    public static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled) key.SetValue(StartupValueName, StartupCommandForCurrentExecutable(), RegistryValueKind.String);
        else key.DeleteValue(StartupValueName, throwOnMissingValue: false);
    }

    private static string StartupCommandForCurrentExecutable()
    {
        var executable = Environment.ProcessPath ?? throw new IOException("无法取得程序路径。");
        if (!string.Equals(Path.GetFileName(executable), "WorkGuard.exe", StringComparison.OrdinalIgnoreCase))
            throw new IOException("请从发布后的 WorkGuard.exe 开启开机启动。");
        return $"\"{Path.GetFullPath(executable)}\"";
    }

    public static void OpenDataFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
