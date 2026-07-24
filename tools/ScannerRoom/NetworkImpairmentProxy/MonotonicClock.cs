using System.Diagnostics;

namespace ScannerRoom.NetworkImpairmentProxy;

internal interface IMonotonicClock
{
    long Milliseconds { get; }
}

internal sealed class StopwatchClock : IMonotonicClock
{
    private readonly long startedAt = Stopwatch.GetTimestamp();

    public long Milliseconds => (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
}

internal sealed class ManualClock : IMonotonicClock
{
    public long Milliseconds { get; private set; }

    public void Advance(long milliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(milliseconds);
        Milliseconds = checked(Milliseconds + milliseconds);
    }
}
