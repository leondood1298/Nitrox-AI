using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using NitroxClient.MonoBehaviours;

namespace NitroxClient.GameLogic;

/// <summary>
///     Pairs each crash home with its crashfish. The crashfish is a stored entity of its cell, so it and its home are
///     loaded and unloaded separately: a home whose fish is not here yet waits for it instead of making another one.
///     When a crashfish is gone for good the server tells its home when to make the next (CrashHomeRespawns).
/// </summary>
public sealed class CrashHomes
{
    private readonly Dictionary<NitroxId, NitroxId> crashIdByHomeId = [];

    /// <summary>
    ///     Records which crashfish the home has, from its metadata, and pairs them if the fish is here.
    /// </summary>
    public void Apply(CrashHome home, NitroxId homeId, Optional<NitroxId> crashId)
    {
        if (!crashId.HasValue)
        {
            crashIdByHomeId.Remove(homeId);
            return;
        }
        crashIdByHomeId[homeId] = crashId.Value;
        TryPair(home);
    }

    /// <summary>
    ///     Called while the home has no crashfish: pairs it with its own as soon as that one is spawned here.
    /// </summary>
    public void TryPair(CrashHome home)
    {
        if (home.crash || !home.TryGetNitroxId(out NitroxId homeId) || !crashIdByHomeId.TryGetValue(homeId, out NitroxId crashId))
        {
            return;
        }
        if (NitroxEntity.TryGetComponentFrom(crashId, out Crash crash))
        {
            home.crash = crash;
        }
    }

    public void Forget(NitroxId homeId) => crashIdByHomeId.Remove(homeId);
}
