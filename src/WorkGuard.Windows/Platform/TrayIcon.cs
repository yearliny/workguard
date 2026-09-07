using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace WorkGuard.Windows.Platform;

internal sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Drawing.Icon _drawingIcon;
    private readonly Forms.ToolStripMenuItem _status = new("正在启动…") { Enabled = false };
    private readonly Forms.ToolStripMenuItem _meeting = new("会议模式（仅静默计时）") { CheckOnClick = true };
    private readonly Forms.ContextMenuStrip _menu = new();
    private readonly Forms.ToolStripMenuItem _pause = new("暂停提醒 1 小时");

    public TrayIcon(AppController app)
    {
        // A native vector-drawn leaf mark; no external assets or network dependencies.
        using var bitmap = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Drawing.Color.Transparent);
            using var green = new Drawing.SolidBrush(Drawing.Color.FromArgb(50, 105, 88));
            using var white = new Drawing.Pen(Drawing.Color.White, 2.4f);
            g.FillEllipse(green, 1, 1, 30, 30);
            g.DrawArc(white, 9, 7, 14, 17, 30, 220);
            g.DrawLine(white, 13, 23, 20, 10);
        }
        var handle = bitmap.GetHicon();
        try { using var native = Drawing.Icon.FromHandle(handle); _drawingIcon = (Drawing.Icon)native.Clone(); }
        finally { DestroyIcon(handle); }

        _menu.Items.Add(_status);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("打开今日概览", null, (_, _) => app.ShowDashboard());
        _menu.Items.Add("20 秒眼睛休息", null, (_, _) => app.StartBreak(BreakKind.Eyes));
        _menu.Items.Add("3 分钟身体活动", null, (_, _) => app.StartBreak(BreakKind.Movement));
        _menu.Items.Add("7 分钟办公室活动", null, (_, _) => app.StartBreak(BreakKind.Office));
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _meeting.CheckedChanged += (_, _) => app.MeetingMode = _meeting.Checked;
        _menu.Items.Add(_meeting);
        _pause.Click += (_, _) => app.TogglePause();
        _menu.Items.Add(_pause);
        _menu.Items.Add("设置", null, (_, _) => app.ShowSettings());
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("退出", null, (_, _) => { app.EndBreakForSystem(); Application.Current.Shutdown(); });
        _icon = new Forms.NotifyIcon { Icon = _drawingIcon, Text = "工作防沉迷", ContextMenuStrip = _menu, Visible = true };
        _icon.DoubleClick += (_, _) => app.ShowDashboard();
        _icon.BalloonTipClicked += (_, _) => app.ShowDashboard();
    }

    public void Update(TimeSpan continuous, TimeSpan due, bool paused, bool meeting)
    {
        _status.Text = $"连续约 {continuous.TotalMinutes:0} 分钟 · 下次活动 {Math.Ceiling(due.TotalMinutes):0} 分钟";
        _icon.Text = $"工作防沉迷 · 连续约 {continuous.TotalMinutes:0} 分钟";
        _pause.Text = paused ? "恢复提醒" : "暂停提醒 1 小时";
        _meeting.Checked = meeting;
    }

    public void Notify(string title, string message) => _icon.ShowBalloonTip(5000, title, message, Forms.ToolTipIcon.Info);
    public void Dispose() { _icon.Visible = false; _icon.Dispose(); _menu.Dispose(); _drawingIcon.Dispose(); }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
