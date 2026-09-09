using System.Net.Http;
using System.Threading.Tasks;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace WorkGuard.Windows.Platform;

internal sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Drawing.Icon _drawingIcon;
    private readonly UpdateService _updates;
    private readonly AppController _app;
    private readonly Forms.ToolStripMenuItem _status = new("正在启动…") { Enabled = false };
    private readonly Forms.ToolStripMenuItem _detail = new("请稍候") { Enabled = false };
    private readonly Forms.ToolStripMenuItem _meeting = new("会议模式（仅静默计时）") { CheckOnClick = true };
    private readonly Forms.ContextMenuStrip _menu = new();
    private readonly Forms.ToolStripMenuItem _pause = new("暂停提醒 1 小时");
    private readonly Forms.ToolStripMenuItem _update = new("检查更新");
    private PreparedUpdate? _preparedUpdate;
    private bool _checking;

    public TrayIcon(AppController app)
    {
        _app = app;
        _updates = new UpdateService(app.DataDirectory);
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
        _update.Click += (_, _) =>
        {
            if (_preparedUpdate is not null) InstallPreparedUpdate();
            else _ = CheckForUpdatesAsync(force: true);
        };
        _menu.Items.Add(_update);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("退出", null, (_, _) => { app.EndBreakForSystem(); Application.Current.Shutdown(); });
        _icon = new Forms.NotifyIcon { Icon = _drawingIcon, Text = "工作防沉迷", ContextMenuStrip = _menu, Visible = true };
        _icon.DoubleClick += (_, _) => app.ShowDashboard();
        _icon.BalloonTipClicked += (_, _) => app.ShowDashboard();
        _ = CheckForUpdatesAsync(force: false);
    }

    private async Task CheckForUpdatesAsync(bool force)
    {
        if (_checking) return;
        _checking = true;
        if (force)
        {
            _update.Enabled = false;
            _update.Text = "正在检查更新…";
        }
        try
        {
            var prepared = await _updates.CheckAndPrepareAsync(force);
            _preparedUpdate = prepared;
            _update.Enabled = true;
            if (prepared is null)
            {
                _update.Text = "检查更新";
                if (force) Notify("已经是最新版本", $"当前版本 {UpdateService.CurrentVersion()}。");
            }
            else
            {
                _update.Text = $"安装更新 {prepared.Version}";
                Notify("新版本已准备好", $"WorkGuard {prepared.Version} 已下载并校验完成。可从托盘菜单安装。");
            }
        }
        catch (Exception error) when (error is HttpRequestException or IOException or InvalidDataException or System.Text.Json.JsonException or TaskCanceledException)
        {
            Diagnostics.Record(error);
            _preparedUpdate = null;
            _update.Enabled = true;
            _update.Text = "检查更新";
            if (force) Notify("暂时无法检查更新", "网络或更新服务暂不可用，稍后再试即可。");
        }
        finally
        {
            _checking = false;
        }
    }

    private void InstallPreparedUpdate()
    {
        var update = _preparedUpdate;
        if (update is null) return;
        try
        {
            if (!_updates.LaunchInstaller(update))
            {
                Notify("更新包不可用", "请重新检查更新。");
                _preparedUpdate = null;
                _update.Text = "检查更新";
                return;
            }
            _app.EndBreakForSystem();
            Application.Current.Shutdown();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Diagnostics.Record(error);
            Notify("无法启动更新", "更新包已保留，可稍后从托盘菜单重试。");
        }
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
    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _drawingIcon.Dispose();
        _updates.Dispose();
    }
}
