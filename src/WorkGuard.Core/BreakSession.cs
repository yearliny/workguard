namespace WorkGuard.Core;

public sealed class BreakSession(BreakKind kind, bool gentle, bool strict = false, int variant = 0, bool neck = false, bool freeRest = false, bool standing = true)
{
    public BreakKind Kind { get; } = kind;
    public IReadOnlyList<Exercise> Exercises { get; } = freeRest && kind != BreakKind.Eyes ? Programs.FreeRest(kind, standing) : Programs.For(kind, gentle, variant, neck);
    public bool Strict { get; } = strict;
    public bool RestOnly { get; private set; }
    public bool ActivityCompleted => FullyCompleted && !RestOnly;
    public void UseRestAlternative() { if (!Finished) RestOnly = true; }
    public int Index { get; private set; }
    public TimeSpan Observed { get; private set; }
    public TimeSpan StepElapsed { get; private set; }
    public bool Paused { get; set; }
    public bool Finished => Index >= Exercises.Count;
    public bool FullyCompleted => Finished && Observed >= Programs.Duration(Kind);
    public Exercise Current => Exercises[Math.Min(Index, Exercises.Count - 1)];
    public TimeSpan Remaining => Finished ? TimeSpan.Zero : TimeSpan.FromSeconds(Current.Seconds) - StepElapsed;

    // Timeline includes skipped portions, while Observed remains actual time only.
    public TimeSpan TimelineElapsed => TimeSpan.FromSeconds(Exercises.Take(Index).Sum(x => x.Seconds)) + StepElapsed;
    public TimeSpan TotalRemaining => Finished ? TimeSpan.Zero : TimeSpan.FromSeconds(Exercises.Skip(Index).Sum(x => x.Seconds)) - StepElapsed;

    public void Advance(TimeSpan elapsed)
    {
        if (Paused || Finished || elapsed <= TimeSpan.Zero || elapsed > TimeSpan.FromSeconds(10)) return;
        while (elapsed > TimeSpan.Zero && !Finished)
        {
            var take = elapsed < Remaining ? elapsed : Remaining;
            StepElapsed += take;
            Observed += take;
            elapsed -= take;
            if (Remaining <= TimeSpan.Zero) { Index++; StepElapsed = TimeSpan.Zero; }
        }
    }

    // Skipping never awards time or a completed session.
    public void Skip() { if (!Strict && !Finished) { Index++; StepElapsed = TimeSpan.Zero; } }
}
