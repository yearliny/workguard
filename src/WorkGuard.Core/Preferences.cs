namespace WorkGuard.Core;

public sealed record Preferences
{
    public int EyeIntervalMinutes { get; init; } = 20;
    public int MovementIntervalMinutes { get; init; } = 60;
    public int NaturalRestMinutes { get; init; } = 5;
    public bool EyeReminders { get; init; } = true;
    public bool InferNaturalRest { get; init; } = true;
    public bool QuietWhenFullscreen { get; init; } = true;
    public bool GentleOnly { get; init; } = true;
    public bool StartWithWindows { get; init; }
    public bool OnboardingComplete { get; init; }

    public Preferences Validate() => this with
    {
        EyeIntervalMinutes = Math.Clamp(EyeIntervalMinutes, 10, 60),
        MovementIntervalMinutes = Math.Clamp(MovementIntervalMinutes, 30, 120),
        NaturalRestMinutes = Math.Clamp(NaturalRestMinutes, 3, 15)
    };
}
