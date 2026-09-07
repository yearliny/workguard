namespace WorkGuard.Core;

public enum Reminder { None, MovementSoon, Eyes, Movement }
public enum BreakKind { Eyes, Movement, Office }

// Quiet affects delivery, not accumulated exposure. No keyboard contents are collected.
public readonly record struct ActivitySample(TimeSpan IdleAge, bool Unavailable = false,
    bool Quiet = false, bool KeepCountingWithoutInput = false);

public sealed class BreakEngine(Preferences preferences)
{
    public Preferences Preferences { get; private set; } = preferences.Validate();
    public TimeSpan Continuous { get; private set; }
    public TimeSpan EyeExposure { get; private set; }
    public TimeSpan TotalActive { get; private set; }
    public TimeSpan Longest { get; private set; }
    public TimeSpan MovementDueIn => MaxZero(TimeSpan.FromMinutes(Preferences.MovementIntervalMinutes) - Continuous);
    private TimeSpan _snooze;
    private TimeSpan _away;
    private bool _soonSent;

    public void Configure(Preferences preferences) => Preferences = preferences.Validate();
    public void Snooze(TimeSpan duration) => _snooze = MaxZero(duration);

    public Reminder Advance(TimeSpan elapsed, ActivitySample sample)
    {
        // A stalled dispatcher or unexplained clock gap is not observed work or verified rest.
        if (elapsed <= TimeSpan.Zero || elapsed > TimeSpan.FromSeconds(10)) return Reminder.None;
        _snooze = MaxZero(_snooze - elapsed);
        if (sample.Unavailable)
        {
            _away += elapsed;
            ObserveAbsence(_away);
            return Reminder.None;
        }
        _away = TimeSpan.Zero;
        if (!sample.KeepCountingWithoutInput && Preferences.InferNaturalRest)
        {
            if (sample.IdleAge >= TimeSpan.FromMinutes(Preferences.NaturalRestMinutes))
            {
                ResetExposure();
                return Reminder.None;
            }
            // One-minute grace includes ordinary reading/short pauses. Longer idle is uncertain.
            if (sample.IdleAge >= TimeSpan.FromMinutes(1)) return Reminder.None;
        }
        TotalActive += elapsed;
        Continuous += elapsed;
        EyeExposure += elapsed;
        if (Continuous > Longest) Longest = Continuous;
        if (sample.Quiet || _snooze > TimeSpan.Zero) return Reminder.None;

        // Coalesce reminders: a body break also rests the eyes.
        if (MovementDueIn == TimeSpan.Zero)
        {
            _snooze = TimeSpan.FromMinutes(5);
            return Reminder.Movement;
        }
        if (!_soonSent && Continuous >= TimeSpan.FromMinutes(Preferences.MovementIntervalMinutes - 10))
        {
            _soonSent = true;
            _snooze = TimeSpan.FromSeconds(30);
            return Reminder.MovementSoon;
        }
        if (Preferences.EyeReminders && EyeExposure >= TimeSpan.FromMinutes(Preferences.EyeIntervalMinutes))
        {
            _snooze = TimeSpan.FromMinutes(5);
            return Reminder.Eyes;
        }
        return Reminder.None;
    }

    // Call only for an observed Windows lock/suspend interval, never a meeting or app pause.
    public void ObserveAbsence(TimeSpan duration)
    {
        if (duration >= TimeSpan.FromMinutes(Preferences.NaturalRestMinutes)) ResetExposure();
    }

    public bool Complete(BreakKind kind, TimeSpan observedDuration)
    {
        if (observedDuration < Programs.Duration(kind)) return false;
        EyeExposure = TimeSpan.Zero;
        if (kind != BreakKind.Eyes) ResetExposure();
        else _snooze = TimeSpan.Zero;
        return true;
    }

    private void ResetExposure()
    {
        Continuous = TimeSpan.Zero;
        EyeExposure = TimeSpan.Zero;
        _soonSent = false;
        _snooze = TimeSpan.Zero;
    }
    private static TimeSpan MaxZero(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;
}
