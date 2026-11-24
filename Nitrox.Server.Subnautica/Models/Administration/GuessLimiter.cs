using System.Collections.Generic;

namespace Nitrox.Server.Subnautica.Models.Administration;

/// <summary>
///     Slows down guessing a password: after a few wrong tries from an address or a connection, each further try is refused
///     until a wait that doubles with every failure has passed. A right password clears the count.
/// </summary>
internal sealed class GuessLimiter(TimeProvider? timeProvider = null)
{
    /// <summary>
    ///     Wrong tries allowed before any wait, so that a typo costs nothing.
    /// </summary>
    public const int FREE_FAILURES = 3;

    public static readonly TimeSpan FIRST_WAIT = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan LONGEST_WAIT = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     How long a count is kept after the last failure.
    /// </summary>
    private static readonly TimeSpan FORGET_AFTER = TimeSpan.FromHours(1);

    private readonly Dictionary<string, Entry> entries = [];
    private readonly Lock entriesLock = new();
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    /// <param name="keys">What the try is counted against, such as the address and the connection.</param>
    /// <returns>How long the caller must still wait before a try is looked at; zero if it may try now.</returns>
    public TimeSpan GetRemainingWait(params string[] keys)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        TimeSpan longest = TimeSpan.Zero;
        lock (entriesLock)
        {
            foreach (string key in keys)
            {
                if (entries.TryGetValue(key, out Entry? entry) && entry.BlockedUntil > now && entry.BlockedUntil - now > longest)
                {
                    longest = entry.BlockedUntil - now;
                }
            }
        }
        return longest;
    }

    public void RecordFailure(params string[] keys)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        lock (entriesLock)
        {
            ForgetOld(now);
            foreach (string key in keys)
            {
                if (!entries.TryGetValue(key, out Entry? entry))
                {
                    entries[key] = entry = new Entry();
                }
                entry.Failures++;
                entry.LastFailure = now;
                if (entry.Failures >= FREE_FAILURES)
                {
                    entry.BlockedUntil = now + GetWait(entry.Failures);
                }
            }
        }
    }

    public void RecordSuccess(params string[] keys)
    {
        lock (entriesLock)
        {
            foreach (string key in keys)
            {
                entries.Remove(key);
            }
        }
    }

    /// <summary>
    ///     The wait after the <paramref name="failures" />-th failure: doubling from <see cref="FIRST_WAIT" /> up to <see cref="LONGEST_WAIT" />.
    /// </summary>
    internal static TimeSpan GetWait(int failures)
    {
        if (failures < FREE_FAILURES)
        {
            return TimeSpan.Zero;
        }
        int doublings = Math.Min(failures - FREE_FAILURES, 20);
        TimeSpan wait = FIRST_WAIT * Math.Pow(2, doublings);
        return wait > LONGEST_WAIT ? LONGEST_WAIT : wait;
    }

    private void ForgetOld(DateTimeOffset now)
    {
        List<string>? old = null;
        foreach ((string key, Entry entry) in entries)
        {
            if (now - entry.LastFailure > FORGET_AFTER && entry.BlockedUntil < now)
            {
                (old ??= []).Add(key);
            }
        }
        foreach (string key in old ?? [])
        {
            entries.Remove(key);
        }
    }

    private sealed class Entry
    {
        public int Failures;
        public DateTimeOffset LastFailure;
        public DateTimeOffset BlockedUntil;
    }
}
