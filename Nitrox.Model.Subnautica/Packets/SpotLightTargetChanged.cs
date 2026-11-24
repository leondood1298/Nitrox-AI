using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     The creature a base spotlight follows, chosen by the game that simulates the spotlight. Null: it sweeps.
/// </summary>
[Serializable]
public sealed class SpotLightTargetChanged : Packet
{
    public NitroxId SpotLightId { get; }
    public NitroxId? TargetId { get; }

    public SpotLightTargetChanged(NitroxId spotLightId, NitroxId? targetId)
    {
        SpotLightId = spotLightId;
        TargetId = targetId;
    }
}
