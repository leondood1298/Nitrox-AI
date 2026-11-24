using System.Collections.Generic;
using Nitrox.Model.Core;

namespace Nitrox.Server.Subnautica.Models.Administration;

/// <summary>
///     Lets each player send a short burst of chat messages, then one every so often: a token bucket per session.
/// </summary>
internal sealed class ChatRateLimiter(TimeProvider? timeProvider = null)
{
    /// <summary>
    ///     Messages a player may send at once.
    /// </summary>
    public const int BURST = 5;

    /// <summary>
    ///     Time it takes to earn one more message.
    /// </summary>
    public static readonly TimeSpan REFILL_PERIOD = TimeSpan.FromSeconds(2);

    private readonly Dictionary<SessionId, Bucket> buckets = [];
    private readonly Lock bucketsLock = new();
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    /// <returns>True if the player may send a message now, which then uses up one.</returns>
    public bool TryTake(SessionId sessionId)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        lock (bucketsLock)
        {
            if (!buckets.TryGetValue(sessionId, out Bucket? bucket))
            {
                buckets[sessionId] = bucket = new Bucket { Tokens = BURST, LastRefill = now };
            }
            double earned = (now - bucket.LastRefill) / REFILL_PERIOD;
            if (earned > 0)
            {
                bucket.Tokens = Math.Min(BURST, bucket.Tokens + earned);
                bucket.LastRefill = now;
            }
            if (bucket.Tokens < 1)
            {
                return false;
            }
            bucket.Tokens--;
            return true;
        }
    }

    private sealed class Bucket
    {
        public double Tokens;
        public DateTimeOffset LastRefill;
    }
}
