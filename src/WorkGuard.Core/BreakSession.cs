namespace WorkGuard.Core;

public sealed class BreakSession(BreakKind kind, bool gentle)
{
    public BreakKind Kind { get; } = kind;
    public IReadOnlyList<Exercise> Exercises { get; } = Programs.For(kind, gentle);
    public int Index { get; private set; }
    public TimeSpan Observed { get; private set; }
    public TimeSpan StepElapsed { get; private set; }
    public bool Paused { get; set; }
    public bool Finished => Index >= Exercises.Count;
    public bool FullyCompleted => Finished && Observed >= Programs.Duration(Kind);
    public Exercise Current => Exercises[Math.Min(Index, Exercises.Count - 1)];
    public TimeSpan Remaining => Finished ? TimeSpan.Zero : TimeSpan.FromSeconds(Current.Seconds) - StepElapsed;

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
    public void Skip() { if (!Finished) { Index++; StepElapsed = TimeSpan.Zero; } }
}
