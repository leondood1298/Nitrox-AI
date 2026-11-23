using System.Collections.Generic;
using Nitrox.Model.DataStructures;

namespace NitroxClient.GameLogic;

/// <summary>
///     Adds up how much the local player has doused each fire so it can be sent in a few packets instead of one per
///     extinguisher hit (each hit is 0.15 to 0.25, many times a second).
/// </summary>
internal sealed class FireDouseLedger
{
    /// <summary>
    ///     About half a second of continuous extinguishing.
    /// </summary>
    internal const float REPORT_THRESHOLD = 5f;

    private readonly Dictionary<NitroxId, float> pendingByFire = [];

    /// <summary>
    ///     Records a douse and returns the amount to send now, or 0 to keep accumulating. When the fire has gone out
    ///     whatever is pending is always sent, so other players never keep a fire that is out here.
    /// </summary>
    public float Record(NitroxId fireId, float amount, bool extinguished)
    {
        pendingByFire.TryGetValue(fireId, out float pending);
        pending += amount;
        if (extinguished)
        {
            pendingByFire.Remove(fireId);
            return pending;
        }
        if (pending > REPORT_THRESHOLD)
        {
            pendingByFire[fireId] = 0f;
            return pending;
        }
        pendingByFire[fireId] = pending;
        return 0f;
    }
}
