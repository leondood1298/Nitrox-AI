using System.Collections.Generic;
using System.Diagnostics;
using Nitrox.Model.Core;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Bounds how many pings a player can send. The game keeps three of a player's pings at a time and lets one be sent
///     every half second; a modified client could otherwise fill everybody's screen.
/// </summary>
internal sealed class PlayerPingLimiter
{
    /// <summary>
    ///     How many pings a player may send within <see cref="WINDOW" />: the three the game shows and a little more.
    /// </summary>
    public const int BURST = 5;

    public static readonly TimeSpan WINDOW = TimeSpan.FromSeconds(5);

    private readonly Func<TimeSpan> clock;
    private readonly Lock locker = new();
    private readonly Dictionary<SessionId, Queue<TimeSpan>> sent = [];

    public PlayerPingLimiter() : this(CreateClock())
    {
    }

    internal PlayerPingLimiter(Func<TimeSpan> clock)
    {
        this.clock = clock;
    }

    /// <returns>Whether the ping may be relayed; a refused ping does not count.</returns>
    public bool TryPing(SessionId sessionId)
    {
        lock (locker)
        {
            TimeSpan now = clock();
            if (!sent.TryGetValue(sessionId, out Queue<TimeSpan>? times))
            {
                times = new Queue<TimeSpan>();
                sent[sessionId] = times;
            }
            while (times.Count > 0 && now - times.Peek() >= WINDOW)
            {
                times.Dequeue();
            }
            if (times.Count >= BURST)
            {
                return false;
            }
            times.Enqueue(now);
            return true;
        }
    }

    private static Func<TimeSpan> CreateClock()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        return () => stopwatch.Elapsed;
    }
}
