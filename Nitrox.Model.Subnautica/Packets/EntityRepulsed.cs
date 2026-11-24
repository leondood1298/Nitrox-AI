using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     A player's repulsion cannon shot an object that another player simulates. Only that player's game moves it (its
///     position is sent to the others from there), so it is given the shot: the velocity the cannon gave the object.
/// </summary>
[Serializable]
public class EntityRepulsed : Packet
{
    public NitroxId Id { get; }
    public NitroxVector3 Velocity { get; }

    public EntityRepulsed(NitroxId id, NitroxVector3 velocity)
    {
        Id = id;
        Velocity = velocity;
    }
}
