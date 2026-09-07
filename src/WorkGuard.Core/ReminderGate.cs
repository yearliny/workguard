namespace WorkGuard.Core;

public enum DeliveryReason
{
    Ready, Setup, Unavailable, Resting, Settings, Meeting, Paused,
    OutsideSchedule, QuietHours, Fullscreen, Idle, ReminderOpen, Returning
}

// Restores delivery gradually after suppression; health exposure is owned by BreakEngine.
// Observed monotonic ticks only: sleep/stalls never consume the return buffer.
public sealed class ReminderGate
{
    public static readonly TimeSpan ReturnBuffer = TimeSpan.FromSeconds(15);
    public DeliveryReason Reason { get; private set; } = DeliveryReason.Ready;
    public TimeSpan Remaining { get; private set; }
    public bool CanDeliver => Reason == DeliveryReason.Ready;
    private bool _blocked;

    public void Advance(TimeSpan elapsed, DeliveryReason reason)
    {
        if (reason != DeliveryReason.Ready)
        {
            Reason = reason;
            _blocked = true;
            Remaining = ReturnBuffer;
            return;
        }
        if (elapsed > TimeSpan.FromSeconds(10)) { _blocked = false; Remaining = ReturnBuffer; }
        else if (_blocked) _blocked = false; // The previous interval was still suppressed.
        else if (Remaining > TimeSpan.Zero)
        {
            if (elapsed > TimeSpan.Zero) Remaining = elapsed >= Remaining ? TimeSpan.Zero : Remaining - elapsed;
        }
        Reason = Remaining > TimeSpan.Zero ? DeliveryReason.Returning : DeliveryReason.Ready;
    }
}
