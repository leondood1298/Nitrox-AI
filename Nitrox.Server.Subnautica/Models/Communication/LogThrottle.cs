namespace Nitrox.Server.Subnautica.Models.Communication;

/// <summary>
///     Lets a few lines through per time window and counts the rest, for errors a stranger on the open port can cause as
///     often as they like.
/// </summary>
internal sealed class LogThrottle(int linesPerWindow, TimeSpan window)
{
    private readonly Lock gate = new();
    private readonly int linesPerWindow = linesPerWindow;
    private readonly TimeSpan window = window;
    private int linesInWindow;
    private int suppressed;
    private DateTimeOffset windowStart = DateTimeOffset.MinValue;

    /// <param name="suppressedBefore">Lines held back since the last line that was let through.</param>
    public bool ShouldLog(DateTimeOffset now, out int suppressedBefore)
    {
        lock (gate)
        {
            if (now - windowStart >= window)
            {
                windowStart = now;
                linesInWindow = 0;
            }
            if (linesInWindow >= linesPerWindow)
            {
                suppressed++;
                suppressedBefore = 0;
                return false;
            }
            linesInWindow++;
            suppressedBefore = suppressed;
            suppressed = 0;
            return true;
        }
    }
}
