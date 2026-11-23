using System;
using System.Collections.Generic;
using System.Linq;

namespace NitroxClient.GameLogic;

/// <summary>
///     The game times at which the game acts on the Aurora: its four warnings (AuroraWarnings.Update) and the steps of its
///     explosion (CrashedShipExploder.Update). Each acts in the frame in which the clock goes above its time, and only on
///     the first time it finds of what that frame passed, so a time skip that passes several would only act on the last
///     warning, or only swap the ship's model without the explosion.
/// </summary>
public static class AuroraTimeline
{
    /// <summary>
    ///     The game times to step the clock through, in order, so that a jump from <paramref name="from" /> to
    ///     <paramref name="to" /> acts on each time it passes. Each is just above one of the times.
    /// </summary>
    internal static List<double> GetStepsPassed(double from, double to, float timeToStartWarning, float timeToStartCountdown)
    {
        return GetTimes(timeToStartWarning, timeToStartCountdown).Where(time => time >= from && time < to)
                                                                 .OrderBy(time => time)
                                                                 .Select(time => (double)JustAbove(time))
                                                                 .ToList();
    }

    /// <remarks>Computed in single precision like the game does, with Mathf.Lerp's arithmetic.</remarks>
    private static IEnumerable<float> GetTimes(float timeToStartWarning, float timeToStartCountdown)
    {
        float span = timeToStartCountdown - timeToStartWarning;
        // AuroraWarnings.Update: the first three warnings
        yield return timeToStartWarning + span * 0.2f;
        yield return timeToStartWarning + span * 0.5f;
        yield return timeToStartWarning + span * 0.8f;
        // The last warning, and CrashedShipExploder.Update: the countdown sound, the explosion's sound, its effects and
        // damage, and the swap to the exploded ship
        yield return timeToStartCountdown;
        yield return timeToStartCountdown + 24f;
        yield return timeToStartCountdown + 25f;
        yield return timeToStartCountdown + 27f;
    }

    /// <summary>
    ///     The smallest single precision time above <paramref name="time" />, which the game's checks (ScalarMonitor.JustWentAbove)
    ///     take as passed and the next time as not yet reached.
    /// </summary>
    internal static float JustAbove(float time)
    {
        int bits = BitConverter.ToInt32(BitConverter.GetBytes(time), 0);
        // Game times are positive: the next representable value has the next bit pattern
        return BitConverter.ToSingle(BitConverter.GetBytes(bits + 1), 0);
    }
}
