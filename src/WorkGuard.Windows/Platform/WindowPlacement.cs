using System.Runtime.InteropServices;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace WorkGuard.Windows.Platform;

internal static class WindowPlacement
{
    public static void Place(Window window, bool fullscreen, bool corner = false)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        var bounds = fullscreen ? screen.Bounds : screen.WorkingArea;
        var scale = HwndSource.FromHwnd(handle)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var width = fullscreen ? bounds.Width : Math.Min((int)(window.Width * scale.M11), bounds.Width);
        var height = fullscreen ? bounds.Height : Math.Min((int)(window.Height * scale.M22), bounds.Height);
        var margin = corner ? Math.Min(16, Math.Max(0, bounds.Width - width)) : 0;
        var left = corner ? bounds.Right - width - margin : bounds.Left + (bounds.Width - width) / 2;
        var top = corner ? Math.Max(bounds.Top, bounds.Bottom - height - 16) : bounds.Top + (bounds.Height - height) / 2;
        SetWindowPos(handle, new IntPtr(-1), left, top, width, height, 0x0010);
    }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int width, int height, uint flags);
}
