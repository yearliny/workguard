namespace WorkGuard.Core;

// Quick setup edits only the choices it exposes; advanced preferences are preserved.
public sealed record QuickSetup(int? MovementMinutes, bool Strict, bool Sound)
{
    public Preferences ApplyTo(Preferences current) => (current with
    {
        EyeIntervalMinutes = MovementMinutes is null ? current.EyeIntervalMinutes : 20,
        MovementIntervalMinutes = MovementMinutes ?? current.MovementIntervalMinutes,
        StrictMode = Strict,
        SoundEnabled = Sound,
        OnboardingComplete = true
    }).Validate();
}
