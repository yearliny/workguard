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
    public StoredState State { get; private set; }
    public BreakEngine Engine { get; }
    private bool _meetingMode;
    public bool MeetingMode
    {
        get => _meetingMode;
        set { _meetingMode = value; if (value) DismissReminder(); Changed?.Invoke(); }
    }
    public TimeSpan PauseRemaining => Paused ? _pauseUntil - _clock.Elapsed : TimeSpan.Zero;
    public string? DataError { get; private set; }
    public bool CanRestoreBackup => _store.CanRestoreBackup;
    public bool QuietHoursActive => State.Preferences.IsQuietTime(TimeOnly.FromDateTime(DateTime.Now));
    public bool Paused => _clock.Elapsed < _pauseUntil;
    public ReminderGate Delivery { get; } = new();
    public string Status => !State.Preferences.OnboardingComplete ? ReminderCopy.Title(DeliveryReason.Setup) : ReminderCopy.Title(Delivery.Reason);
    public string DeliveryDetail => ReminderCopy.Detail(Delivery, State.Preferences, Engine, _localNow, PauseRemaining) +
        (Delivery.CanDeliver && !_idleKnown ? " 空闲状态不可用，暂按用屏时间估计。" : "");
    private DateTime _localNow = DateTime.Now;
    private bool _idleKnown = true;
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
    private WelcomeWindow? _welcome;
    private string? _lastCompletion;
    private TimeSpan _completionUntil;
    public string? LastCompletion => _clock.Elapsed < _completionUntil ? _lastCompletion : null;
    private BreakWindow? _break;
    private ReminderWindow? _reminder;
    private int _eyeVariant, _bodyVariant;

    public AppController(string? dataDirectory = null)
    {
        if (dataDirectory is not null) DataDirectory = dataDirectory;
        _store = new LocalStore(DataDirectory);
        State = _store.Load();
        DataError = _store.LoadWarning;
        Engine = new BreakEngine(State.Preferences);
        Motion.Configure(State.Preferences.ReduceMotion);
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
        if (DataError is not null) ShowDashboard();
        else if (!State.Preferences.OnboardingComplete) ShowWelcome();
    }

    private void Tick(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        var elapsed = now - _lastTick;
        _lastTick = now;
        var quietFullscreen = State.Preferences.QuietWhenFullscreen && WindowsActivity.IsOtherAppFullscreen();
        Advance(elapsed, DateTimeOffset.Now, WindowsActivity.IdleAge(), quietFullscreen, _locked || _sleeping);
        if (now - _lastSave >= TimeSpan.FromSeconds(30)) { Save(); _lastSave = now; }
    }

    // Same controller path is exercised by Windows checks with explicit time and activity samples.
    internal void Advance(TimeSpan elapsed, DateTimeOffset localNow, TimeSpan? idle, bool quietFullscreen, bool unavailable = false)
    {
        _localNow = localNow.DateTime;
        _idleKnown = idle is not null;
        var p = State.Preferences;
        var before = Engine.TotalActive;
        var outside = !WorkSchedule.Allows(p, _localNow);
        var quietHours = p.IsQuietTime(TimeOnly.FromDateTime(_localNow));
        var away = p.InferNaturalRest && !MeetingMode && !quietFullscreen && idle >= TimeSpan.FromMinutes(1);
        if (quietFullscreen || quietHours || outside || Paused || unavailable || MeetingMode || away) DismissReminder();
        var reason = !p.OnboardingComplete ? DeliveryReason.Setup : unavailable ? DeliveryReason.Unavailable :
            _break is not null ? DeliveryReason.Resting : _welcome is not null || _settings is not null ? DeliveryReason.Settings :
            MeetingMode ? DeliveryReason.Meeting : Paused ? DeliveryReason.Paused : outside ? DeliveryReason.OutsideSchedule :
            quietHours ? DeliveryReason.QuietHours : quietFullscreen ? DeliveryReason.Fullscreen : away ? DeliveryReason.Idle :
            _reminder is not null ? DeliveryReason.ReminderOpen : DeliveryReason.Ready;
        Delivery.Advance(elapsed, reason);
        if (unavailable)
        {
            Engine.Advance(elapsed, new(TimeSpan.Zero, Unavailable: true));
            _break?.PauseForInterruption();
        }
        else if (_break is { IsFinished: true })
        {
            // Do not accrue work while the user is still on the completion page.
        }
        else if (_break is { IsRunning: true })
        {
            if (elapsed > TimeSpan.FromSeconds(10)) _break.PauseForInterruption();
            else _break.Tick(elapsed);
        }
        else
        {
            // Reserve and persist the daily offer before showing it. An offer is never activity credit.
            var officeDue = DataError is null && Delivery.CanDeliver && Engine.SnoozeRemaining <= TimeSpan.Zero &&
                elapsed > TimeSpan.Zero && elapsed <= TimeSpan.FromSeconds(10) &&
                WorkSchedule.OfficeDue(p, _localNow, State.LastOfficeReminderDate,
                    State.Days.Any(d => d.Date == DateOnly.FromDateTime(_localNow) && d.OfficeBreaks > 0));
            if (officeDue)
            {
                var previousDate = State.LastOfficeReminderDate;
                State.LastOfficeReminderDate = DateOnly.FromDateTime(_localNow);
                if (Save()) ShowReminder(BreakKind.Office);
                else State.LastOfficeReminderDate = previousDate;
            }
            var quiet = !Delivery.CanDeliver || _reminder is not null;
            // Unavailable idle API disables inference; it must never imply absence.
            var result = Engine.Advance(elapsed, new(idle ?? TimeSpan.Zero, Quiet: quiet,
                KeepCountingWithoutInput: MeetingMode || quietFullscreen || idle is null));
            if (State.Preferences.InferNaturalRest && !MeetingMode && !quietFullscreen &&
                idle >= TimeSpan.FromMinutes(State.Preferences.NaturalRestMinutes) && _break is { HasStarted: false })
                _break.Close();
            if (State.Preferences.InferNaturalRest && !MeetingMode && !quietFullscreen && idle >= TimeSpan.FromMinutes(State.Preferences.NaturalRestMinutes)) DismissReminder();
            if (result == Reminder.MovementSoon)
                _tray?.Notify("稍后，给身体一点时间", "还有约 10 分钟就到活动时间。可以先完成手头这一小段。");
            else if (result is Reminder.Movement or Reminder.Eyes)
            {
                var kind = result == Reminder.Eyes ? BreakKind.Eyes : BreakKind.Movement;
                if (State.Preferences.ShouldForce(kind)) StartBreak(kind, automatic: true, strict: true);
                else ShowReminder(kind);
            }
        }
        Statistics.Record(State, Engine.TotalActive - before, localNow, Engine.Continuous);
        _tray?.Update(Status, DeliveryDetail, Paused, MeetingMode);
        Changed?.Invoke();
    }

    private void ShowReminder(BreakKind kind)
    {
        if (_reminder is not null) return;
        var reminder = new ReminderWindow(kind);
        _reminder = reminder;
        reminder.Closed += (_, _) =>
        {
            _reminder = null;
            if (reminder.Accepted) StartBreak(kind, beginImmediately: true);
            else if (kind != BreakKind.Office) Engine.Snooze(TimeSpan.FromMinutes(5));
        };
        reminder.Show();
    }

    private void DismissReminder() => _reminder?.Close();

    public void StartBreak(BreakKind kind, bool automatic = false, bool beginImmediately = false, bool strict = false)
    {
        if (_locked || _sleeping) return;
        DismissReminder();
        if (_break is not null) { if (!automatic) _break.Activate(); return; }
        _break = new BreakWindow(kind, State.Preferences.GentleOnly, automatic, Engine.Continuous, State.Preferences.SoundEnabled, strict, kind == BreakKind.Eyes ? _eyeVariant++ : _bodyVariant++, State.Preferences.NeckMovements);
        _break.Completed += session =>
        {
            if (session.FullyCompleted && Engine.Complete(session.RestOnly ? BreakKind.Eyes : session.Kind, session.Observed))
            {
                var day = LocalStore.Day(State, DateOnly.FromDateTime(DateTime.Now));
                if (session.Kind == BreakKind.Eyes || session.RestOnly) day.EyeBreaks++;
                else if (session.Kind == BreakKind.Movement) day.MovementBreaks++;
                else day.OfficeBreaks++;
                if (session.RestOnly) Engine.Snooze(TimeSpan.FromMinutes(5));
                _completionUntil = _clock.Elapsed + TimeSpan.FromMinutes(2);
                _lastCompletion = $"{DateTime.Now:HH:mm} · " + (session.RestOnly ? "安静休息已完成，未计为身体活动。" : session.Kind == BreakKind.Eyes ? "完成一次眼睛休息。" : "完成一次身体活动，带着轻松继续。");
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
        var window = _break;
        _lastTick = _clock.Elapsed;
        window.Show();
        if (beginImmediately) window.BeginSession();
    }

    public void ShowDashboard()
    {
        if (_dashboard is not null) { _dashboard.WindowState = WindowState.Normal; _dashboard.Activate(); return; }
        _dashboard = new DashboardWindow(this);
        _dashboard.Closed += (_, _) => _dashboard = null;
        _dashboard.Show();
    }

    public void ShowWelcome()
    {
        if (_welcome is not null) { _welcome.Activate(); return; }
        if (_settings is not null) { _settings.Activate(); return; }
        DismissReminder();
        _welcome = new WelcomeWindow(State.Preferences, ApplyPreferences, () => DataError, ShowDashboard);
        _welcome.Closed += (_, _) => { _welcome = null; Changed?.Invoke(); };
        _welcome.Show();
    }

    public void ShowSettings()
    {
        if (_welcome is not null) { _welcome.Activate(); return; }
        if (_settings is not null) { _settings.Activate(); return; }
        DismissReminder();
        _settings = new SettingsWindow(this);
        _settings.Closed += (_, _) => _settings = null;
        _settings.Show();
    }

    public void ShowScheduleSettings()
    {
        ShowSettings();
        if (_settings is not null) _settings.Sections.SelectedItem = _settings.ScheduleTab;
    }

    public bool ApplyPreferences(Preferences value)
    {
        string? previousStartup = null;
        var startupChanged = false;
        var next = value.Validate() with { OnboardingComplete = true };
        try
        {
            previousStartup = WindowsActivity.StartupCommand();
            if (next.StartWithWindows || previousStartup is not null)
            { WindowsActivity.SetStartup(next.StartWithWindows); startupChanged = true; }
            _store.Save(new StoredState { Preferences = next, Days = State.Days, LastOfficeReminderDate = State.LastOfficeReminderDate });
            State.Preferences = next;
            Motion.Configure(next.ReduceMotion);
            Engine.Configure(next);
            DataError = null;
            DismissReminder();
            Delivery.Advance(TimeSpan.Zero, DeliveryReason.Settings);
            Changed?.Invoke();
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            try { if (startupChanged) WindowsActivity.RestoreStartup(previousStartup); } catch (Exception rollback) { Diagnostics.Record(rollback); }
            DataError = "设置未保存。请检查数据文件或恢复备份后重试。";
            Diagnostics.Record(error);
            Changed?.Invoke();
            return false;
        }
    }

    public void RestoreBackup()
    {
        if (MessageBox.Show("将恢复上一次成功保存的数据。当前文件会另外保留，恢复的设置需要重新确认。", "恢复本地备份", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        try
        {
            State = _store.RestoreBackup();
            Motion.Configure(State.Preferences.ReduceMotion);
            Engine.Configure(State.Preferences);
            DataError = null;
            _welcome?.Close(); // Discard the old draft after replacing preferences from backup.
            _settings?.Close();
            ShowSettings();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { DataError = "恢复失败，原数据仍然保留。请打开数据目录检查。"; Diagnostics.Record(error); }
        Changed?.Invoke();
    }

    public void ExportCsv()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "CSV 表格|*.csv", FileName = $"WorkGuard-{DateTime.Now:yyyy-MM-dd}.csv" };
        if (dialog.ShowDialog() != true) return;
        try { File.WriteAllText(dialog.FileName, Statistics.Csv(State.Days), new System.Text.UTF8Encoding(true)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { Diagnostics.Record(error); MessageBox.Show("无法导出，请选择可写入的位置。", "工作防沉迷"); }
    }

    public void EndBreakForSystem() => _break?.CloseForSystem();

    public void TogglePause() { _pauseUntil = Paused ? TimeSpan.Zero : _clock.Elapsed + TimeSpan.FromHours(1); if (Paused) DismissReminder(); Changed?.Invoke(); }

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
            DismissReminder();
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

    private void DisplayChanged(object? sender, EventArgs e) => OnUi(() => { DismissReminder(); _break?.CloseForSystem(); });
    private void OnUi(Action action) { if (!_disposed) Application.Current.Dispatcher.BeginInvoke(new Action(() => { if (!_disposed) action(); })); }

    public bool Save()
    {
        try
        {
            State.Days.RemoveAll(d => d.Date < DateOnly.FromDateTime(DateTime.Now).AddDays(-89));
            _store.Save(State);
            _saveErrorShown = false;
            DataError = null;
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            if (!_saveErrorShown) _tray?.Notify("统计暂未保存", "本地文件暂时无法写入，稍后会自动重试。");
            if (!_saveErrorShown) Diagnostics.Record(error);
            _saveErrorShown = true;
            DataError = "本地数据暂未保存。请检查目录权限或恢复备份。";
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        DismissReminder();
        SystemEvents.SessionSwitch -= SessionSwitch;
        SystemEvents.PowerModeChanged -= PowerChanged;
        SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        Save();
        _tray?.Dispose();
    }
}
