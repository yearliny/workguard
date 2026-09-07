namespace WorkGuard.Core;

public sealed record Preferences
{
    public int EyeIntervalMinutes { get; init; } = 20;
    public int MovementIntervalMinutes { get; init; } = 60;
    public int NaturalRestMinutes { get; init; } = 5;
    public bool StrictMode { get; init; }
    public bool StrictEyes { get; init; } = true;
    public bool NeckMovements { get; init; }
    public bool ShouldForce(BreakKind kind) => StrictMode &&
        (kind == BreakKind.Movement || kind == BreakKind.Eyes && StrictEyes && EyeReminders);
    public bool EyeReminders { get; init; } = true;
    public bool InferNaturalRest { get; init; } = true;
    public bool QuietWhenFullscreen { get; init; } = true;
    public bool GentleOnly { get; init; } = true;
    public bool StartWithWindows { get; init; }
    public bool OnboardingComplete { get; init; }
    public bool ReduceMotion { get; init; }
    public bool SoundEnabled { get; init; } = true;
    public bool QuietHoursEnabled { get; init; }
    public int QuietStartMinute { get; init; } = 12 * 60;
    public int QuietEndMinute { get; init; } = 13 * 60;
    public bool WorkScheduleEnabled { get; init; }
    // Bits follow DayOfWeek: Sunday=1, Monday=2, ... Saturday=64.
    public int WorkDays { get; init; } = 62;
    public int WorkStartMinute { get; init; } = 9 * 60;
    public int WorkEndMinute { get; init; } = 18 * 60;
    public bool OfficeReminderEnabled { get; init; }
    public int OfficeReminderMinute { get; init; } = 15 * 60;

    public Preferences Validate() => this with
    {
        EyeIntervalMinutes = Math.Clamp(EyeIntervalMinutes, 10, 60),
        MovementIntervalMinutes = Math.Clamp(MovementIntervalMinutes, 30, 120),
        NaturalRestMinutes = Math.Clamp(NaturalRestMinutes, 3, 15),
        QuietStartMinute = Math.Clamp(QuietStartMinute, 0, 1439),
        QuietEndMinute = Math.Clamp(QuietEndMinute, 0, 1439),
        WorkDays = WorkDays & 127,
        WorkStartMinute = Math.Clamp(WorkStartMinute, 0, 1439),
        WorkEndMinute = Math.Clamp(WorkEndMinute, 0, 1439),
        OfficeReminderMinute = Math.Clamp(OfficeReminderMinute, 0, 1439)
    };

    public bool IsQuietTime(TimeOnly time)
    {
        if (!QuietHoursEnabled || QuietStartMinute == QuietEndMinute) return false;
        var minute = time.Hour * 60 + time.Minute;
        return QuietStartMinute < QuietEndMinute
            ? minute >= QuietStartMinute && minute < QuietEndMinute
            : minute >= QuietStartMinute || minute < QuietEndMinute;
    }
}
