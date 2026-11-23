using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     Energy a player took from a base power source that another player simulates. Sent by the consuming client to
///     the server, which passes it on to the source's simulation owner so that the owner's authoritative state (and
///     therefore everyone's) includes it.
/// </summary>
[Serializable]
public sealed class BasePowerSourceConsume : Packet
{
    public NitroxId SourceId { get; }
    public float Amount { get; }

    public BasePowerSourceConsume(NitroxId sourceId, float amount)
    {
        SourceId = sourceId;
        Amount = amount;
    }
}
