using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace WorkGuard.Windows.Platform;

internal sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Drawing.Icon _drawingIcon;
    private readonly Forms.ToolStripMenuItem _status = new("正在启动…") { Enabled = false };
    private readonly Forms.ToolStripMenuItem _detail = new("请稍候") { Enabled = false };
    private readonly Forms.ToolStripMenuItem _meeting = new("会议模式（仅静默计时）") { CheckOnClick = true };
    private readonly Forms.ContextMenuStrip _menu = new();
    private readonly Forms.ToolStripMenuItem _pause = new("暂停提醒 1 小时");

    public TrayIcon(AppController app)
    {
        _drawingIcon = LoadApplicationIcon();

        _menu.Items.Add(_status);
        _menu.Items.Add(_detail);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("打开今日概览", null, (_, _) => app.ShowDashboard());
        _menu.Items.Add("20 秒眼睛休息", null, (_, _) => app.StartBreak(BreakKind.Eyes));
        _menu.Items.Add("身体休息 / 短维护", null, (_, _) => app.StartBreak(BreakKind.Movement));
        _menu.Items.Add("每日身体维护", null, (_, _) => app.ShowMaintenance());
        _menu.Items.Add("7 分钟办公室活动", null, (_, _) => app.StartBreak(BreakKind.Office));
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _meeting.CheckedChanged += (_, _) => app.MeetingMode = _meeting.Checked;
        _menu.Items.Add(_meeting);
        _pause.Click += (_, _) => app.TogglePause();
        _menu.Items.Add(_pause);
        _menu.Items.Add("设置", null, (_, _) => app.ShowSettings());
        _menu.Items.Add("工作日与活动预约", null, (_, _) => app.ShowScheduleSettings());
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("退出", null, (_, _) => { app.EndBreakForSystem(); Application.Current.Shutdown(); });
        _icon = new Forms.NotifyIcon { Icon = _drawingIcon, Text = "工作防沉迷", ContextMenuStrip = _menu, Visible = true };
        _icon.DoubleClick += (_, _) => app.ShowDashboard();
        _icon.BalloonTipClicked += (_, _) => app.ShowDashboard();
    }

    private static Drawing.Icon LoadApplicationIcon()
    {
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            var associated = Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath);
            if (associated is not null)
            {
                using (associated) return (Drawing.Icon)associated.Clone();
            }
        }

        // Defensive fallback for unusual hosts; normal packaged builds use the EXE icon.
        return (Drawing.Icon)Drawing.SystemIcons.Application.Clone();
    }

    public void Update(string status, string detail, bool paused, bool meeting)
    {
        _status.Text = status;
        _detail.Text = detail;
        _icon.Text = $"工作防沉迷 · {status}";
        _pause.Text = paused ? "恢复提醒" : "暂停提醒 1 小时";
        _meeting.Checked = meeting;
    }

    public void Notify(string title, string message) => _icon.ShowBalloonTip(5000, title, message, Forms.ToolTipIcon.Info);
    public void Dispose() { _icon.Visible = false; _icon.Dispose(); _menu.Dispose(); _drawingIcon.Dispose(); }
}
