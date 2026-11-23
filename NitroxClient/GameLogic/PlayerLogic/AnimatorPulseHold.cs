using System.Collections.Generic;

namespace NitroxClient.GameLogic.PlayerLogic;

/// <summary>
///     Keeps an animator switch on for a minimum time. A knife swing turns "using_tool" on for a single frame on the
///     player's own game; the on and off messages can then arrive in the same frame here, and an animator that never
///     sees the switch on never plays the swing.
/// </summary>
public sealed class AnimatorPulseHold(float minimumOnSeconds)
{
    private readonly Dictionary<string, float> switchedOnAt = [];
    private readonly Dictionary<string, float> switchOffAt = [];

    /// <summary>
    ///     Records that <paramref name="parameter" /> should become <paramref name="value" />.
    /// </summary>
    /// <returns>
    ///     True if the animator should be given the value now. False if switching off has to wait; the parameter is then
    ///     returned by <see cref="TakeDue" /> once it has been on for long enough.
    /// </returns>
    public bool Request(string parameter, bool value, float now)
    {
        if (value)
        {
            switchOffAt.Remove(parameter);
            switchedOnAt[parameter] = now;
            return true;
        }

        if (switchedOnAt.TryGetValue(parameter, out float onAt) && now - onAt < minimumOnSeconds)
        {
            switchOffAt[parameter] = onAt + minimumOnSeconds;
            return false;
        }

        switchedOnAt.Remove(parameter);
        return true;
    }

    /// <summary>
    ///     The parameters whose delayed switching off is due. Each is returned once.
    /// </summary>
    public List<string> TakeDue(float now)
    {
        List<string> due = [];
        if (switchOffAt.Count == 0)
        {
            return due;
        }

        foreach (KeyValuePair<string, float> pending in switchOffAt)
        {
            if (now >= pending.Value)
            {
                due.Add(pending.Key);
            }
        }
        foreach (string parameter in due)
        {
            switchOffAt.Remove(parameter);
            switchedOnAt.Remove(parameter);
        }
        return due;
    }

    public void Clear()
    {
        switchedOnAt.Clear();
        switchOffAt.Clear();
    }
}
