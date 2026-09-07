using System.Diagnostics;
using System.Windows.Threading;
using Microsoft.Win32;
using WorkGuard.Data;
using WorkGuard.Windows.Platform;
using WorkGuard.Windows.Views;

namespace WorkGuard.Windows;

internal sealed class AppController : IDisposable
{
    public string DataDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorkGuard");
    public StoredState State { get; }
    public BreakEngine Engine { get; }
    public bool MeetingMode { get; set; }
    public bool Paused => _clock.Elapsed < _pauseUntil;
    public string Status { get; private set; } = "正在观察工作节奏";
    public event Action? Changed;
    private readonly LocalStore _store;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private TimeSpan _lastTick, _lastSave, _pauseUntil;
    private TimeSpan? _unavailableSince;
    private bool _locked, _sleeping, _saveErrorShown, _disposed;
    private TrayIcon? _tray;
    private DashboardWindow? _dashboard;
    private SettingsWindow? _settings;
    private BreakWindow? _break;

    public AppController(string? dataDirectory = null)
    {
        if (dataDirectory is not null) DataDirectory = dataDirectory;
        _store = new LocalStore(DataDirectory);
        State = _store.Load();
        Engine = new BreakEngine(State.Preferences);
    }

    public void Start()
    {
        _tray = new TrayIcon(this);
        SystemEvents.SessionSwitch += SessionSwitch;
        SystemEvents.PowerModeChanged += PowerChanged;
        SystemEvents.DisplaySettingsChanged += DisplayChanged;
        _lastTick = _clock.Elapsed;
        _timer.Tick += Tick;
        _timer.Start();
        if (!State.Preferences.OnboardingComplete) ShowSettings();
        if (_store.LoadWarning is not null) MessageBox.Show(_store.LoadWarning, "工作防沉迷 · 数据提示");
    }

    private void Tick(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        var elapsed = now - _lastTick;
        _lastTick = now;
        var before = Engine.TotalActive;
        var quietFullscreen = State.Preferences.QuietWhenFullscreen && WindowsActivity.IsOtherAppFullscreen();
        if (_locked || _sleeping)
        {
            Engine.Advance(elapsed, new(TimeSpan.Zero, Unavailable: true));
            Status = "已锁屏或休眠";
        }
        else if (_break is { IsRunning: true })
        {
            if (elapsed > TimeSpan.FromSeconds(10)) _break.PauseForInterruption();
            else _break.Tick(elapsed);
            Status = "正在休息";
        }
        else
        {
            var idle = WindowsActivity.IdleAge();
            var quiet = Paused || MeetingMode || quietFullscreen || _break is not null || _settings is not null;
            // Unavailable idle API disables inference; it must never imply absence.
            var result = Engine.Advance(elapsed, new(idle ?? TimeSpan.Zero, Quiet: quiet,
                KeepCountingWithoutInput: MeetingMode || quietFullscreen || idle is null));
            if (State.Preferences.InferNaturalRest && !MeetingMode && !quietFullscreen &&
                idle >= TimeSpan.FromMinutes(State.Preferences.NaturalRestMinutes) && _break is { HasStarted: false })
                _break.Close();
            Status = MeetingMode ? "会议模式 · 静默计时" : Paused ? "提醒已暂停 · 计时继续" :
                quietFullscreen ? "全屏应用 · 静默计时" :
                idle is null ? "无法读取空闲状态 · 按用屏时间估计" :
                State.Preferences.InferNaturalRest && idle >= TimeSpan.FromMinutes(1) ? "暂未检测到输入 · 可能正在离席" : "正在工作 · 记得变换姿势";
            if (result == Reminder.MovementSoon)
                _tray?.Notify("稍后，给身体一点时间", "还有约 10 分钟就到活动时间。可以先完成手头这一小段。");
            else if (result is Reminder.Movement or Reminder.Eyes)
                StartBreak(result == Reminder.Eyes ? BreakKind.Eyes : BreakKind.Movement, automatic: true);
        }
        RecordActive(Engine.TotalActive - before, DateTimeOffset.Now);
        if (now - _lastSave >= TimeSpan.FromSeconds(30)) { Save(); _lastSave = now; }
        _tray?.Update(Engine.Continuous, Engine.MovementDueIn, Paused, MeetingMode);
        Changed?.Invoke();
    }

    private void RecordActive(TimeSpan duration, DateTimeOffset end)
    {
        if (duration <= TimeSpan.Zero) return;
        // Allocate a sample straddling midnight to the correct local dates.
        var cursor = end - duration;
        while (cursor < end)
        {
            var midnight = new DateTimeOffset(cursor.Date.AddDays(1), cursor.Offset);
            var until = end < midnight ? end : midnight;
            var day = LocalStore.Day(State, DateOnly.FromDateTime(cursor.Date));
            day.ActiveSeconds += (until - cursor).TotalSeconds;
            day.LongestSeconds = Math.Max(day.LongestSeconds, Engine.Continuous.TotalSeconds);
            cursor = until;
        }
    }

    public void StartBreak(BreakKind kind, bool automatic = false)
    {
        if (_locked || _sleeping) return;
        if (_break is not null) { if (!automatic) _break.Activate(); return; }
        _break = new BreakWindow(kind, State.Preferences.GentleOnly, automatic, Engine.Continuous);
        _break.Completed += session =>
        {
            if (session.FullyCompleted && Engine.Complete(session.Kind, session.Observed))
            {
                var day = LocalStore.Day(State, DateOnly.FromDateTime(DateTime.Now));
                if (session.Kind == BreakKind.Eyes) day.EyeBreaks++;
                else if (session.Kind == BreakKind.Movement) day.MovementBreaks++;
                else day.OfficeBreaks++;
                Save();
            }
        };
        _break.Ended += session =>
        {
            if (!session.FullyCompleted) Engine.Snooze(TimeSpan.FromMinutes(5));
            _break = null;
            _lastTick = _clock.Elapsed;
            Changed?.Invoke();
        };
        _break.Show();
    }

    public void ShowDashboard()
    {
        if (_dashboard is not null) { _dashboard.WindowState = WindowState.Normal; _dashboard.Activate(); return; }
        _dashboard = new DashboardWindow(this);
        _dashboard.Closed += (_, _) => _dashboard = null;
        _dashboard.Show();
    }

    public void ShowSettings()
    {
        if (_settings is not null) { _settings.Activate(); return; }
        _settings = new SettingsWindow(this);
        _settings.Closed += (_, _) => _settings = null;
        _settings.Show();
    }

    public bool ApplyPreferences(Preferences value)
    {
        try
        {
            WindowsActivity.SetStartup(value.StartWithWindows);
            State.Preferences = value.Validate() with { OnboardingComplete = true };
            Engine.Configure(State.Preferences);
            Save();
            Changed?.Invoke();
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            MessageBox.Show("无法更新开机启动设置：" + error.Message, "工作防沉迷");
            return false;
        }
    }

    public void TogglePause() { _pauseUntil = Paused ? TimeSpan.Zero : _clock.Elapsed + TimeSpan.FromHours(1); Changed?.Invoke(); }

    private void SessionSwitch(object sender, SessionSwitchEventArgs e) => OnUi(() =>
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect)
            _locked = true;
        else if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect)
            _locked = false;
        UpdateAvailability();
    });

    private void PowerChanged(object sender, PowerModeChangedEventArgs e) => OnUi(() =>
    {
        if (e.Mode == PowerModes.Suspend) _sleeping = true;
        else if (e.Mode == PowerModes.Resume) _sleeping = false;
        UpdateAvailability();
    });

    private void UpdateAvailability()
    {
        if (_locked || _sleeping)
        {
            _unavailableSince ??= _clock.Elapsed;
            if (_break is { HasStarted: false }) _break.Close();
            else _break?.PauseForInterruption();
        }
        else if (_unavailableSince is { } since)
        {
            Engine.ObserveAbsence(_clock.Elapsed - since);
            _unavailableSince = null;
        }
        _lastTick = _clock.Elapsed;
    }

    private void DisplayChanged(object? sender, EventArgs e) => OnUi(() => _break?.Close());
    private static void OnUi(Action action) => Application.Current.Dispatcher.BeginInvoke(action);

    public void Save()
    {
        try
        {
            State.Days.RemoveAll(d => d.Date < DateOnly.FromDateTime(DateTime.Now).AddDays(-89));
            _store.Save(State);
            _saveErrorShown = false;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            if (!_saveErrorShown) _tray?.Notify("统计暂未保存", "本地文件暂时无法写入，稍后会自动重试。");
            _saveErrorShown = true;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        SystemEvents.SessionSwitch -= SessionSwitch;
        SystemEvents.PowerModeChanged -= PowerChanged;
        SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        Save();
        _tray?.Dispose();
    }
}
