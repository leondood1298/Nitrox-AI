using System;
using System.Collections.Generic;

namespace Nitrox.Model.Subnautica.Helper;

/// <summary>
///     Thins out repeats of the same diagnostic line. The first two occurrences are written, after that only every
///     power of two, so a request that fails on every packet costs a handful of lines instead of thousands while the
///     written lines still say how often it happened. Not thread safe: callers hold their own lock.
/// </summary>
public sealed class DiagnosticRepeatSampler
{
    private readonly int keyCapacity;
    private readonly Dictionary<string, long> occurrencesByKey;
    private readonly Queue<string> keysInArrivalOrder;

    /// <param name="keyCapacity">
    ///     Keys come from ids and reasons a client can choose, so the oldest key is forgotten beyond this many.
    /// </param>
    public DiagnosticRepeatSampler(int keyCapacity)
    {
        this.keyCapacity = Math.Max(1, keyCapacity);
        occurrencesByKey = new Dictionary<string, long>(this.keyCapacity);
        keysInArrivalOrder = new Queue<string>(this.keyCapacity);
    }

    /// <summary>
    ///     Counts one more occurrence of <paramref name="key" />.
    /// </summary>
    /// <returns>True when this occurrence should be written.</returns>
    public bool Observe(string key, out long occurrences)
    {
        if (!occurrencesByKey.TryGetValue(key, out occurrences))
        {
            if (occurrencesByKey.Count >= keyCapacity)
            {
                occurrencesByKey.Remove(keysInArrivalOrder.Dequeue());
            }
            keysInArrivalOrder.Enqueue(key);
        }
        occurrences++;
        occurrencesByKey[key] = occurrences;
        return IsSampledOccurrence(occurrences);
    }

    public static bool IsSampledOccurrence(long occurrences) => occurrences <= 2 || (occurrences & (occurrences - 1)) == 0;
}
