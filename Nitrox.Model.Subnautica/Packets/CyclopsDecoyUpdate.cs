using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     A creature decoy launched from a Cyclops, as the game of the player who launched it plays it. The other games show a
///     copy: made where it was launched, put where it came to rest, removed when it ends.
/// </summary>
[Serializable]
public sealed class CyclopsDecoyUpdate : Packet
{
    public enum Phase
    {
        /// <summary>Left the launcher at <see cref="Position" /> and rises.</summary>
        LAUNCHED,

        /// <summary>Came to rest at <see cref="Position" />.</summary>
        SETTLED,

        /// <summary>Its lifetime is over.</summary>
        ENDED
    }

    public NitroxId CyclopsId { get; }
    public NitroxId DecoyId { get; }
    public NitroxVector3 Position { get; }
    public Phase DecoyPhase { get; }

    public CyclopsDecoyUpdate(NitroxId cyclopsId, NitroxId decoyId, NitroxVector3 position, Phase decoyPhase)
    {
        CyclopsId = cyclopsId;
        DecoyId = decoyId;
        Position = position;
        DecoyPhase = decoyPhase;
    }

    public override string ToString()
    {
        return $"[{nameof(CyclopsDecoyUpdate)} CyclopsId: {CyclopsId}, DecoyId: {DecoyId}, Position: {Position}, Phase: {DecoyPhase}]";
    }
}
