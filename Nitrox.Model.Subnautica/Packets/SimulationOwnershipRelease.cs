using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     Sent by a player who was given background simulation of an entity it cannot simulate, because the object does
///     not exist on its side. The server then gives the simulation to someone else.
/// </summary>
[Serializable]
public sealed class SimulationOwnershipRelease : Packet
{
    public NitroxId Id { get; }

    public SimulationOwnershipRelease(NitroxId id)
    {
        Id = id;
    }
}
