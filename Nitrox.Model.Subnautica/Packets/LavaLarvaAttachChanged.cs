using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     A Lava Larva attached itself to, or came off, a vehicle or Cyclops in the game simulating the larva. The other games
///     show their copy attached at the same point, and count it on the vehicle (it slows the vehicle down).
/// </summary>
[Serializable]
public sealed class LavaLarvaAttachChanged : Packet
{
    public NitroxId LarvaId { get; }
    public NitroxId VehicleId { get; }

    /// <summary>The index of the point in the vehicle's LavaLarvaTarget.attachPoints.</summary>
    public int AttachPoint { get; }

    public bool Attached { get; }

    public LavaLarvaAttachChanged(NitroxId larvaId, NitroxId vehicleId, int attachPoint, bool attached)
    {
        LarvaId = larvaId;
        VehicleId = vehicleId;
        AttachPoint = attachPoint;
        Attached = attached;
    }

    public override string ToString()
    {
        return $"[{nameof(LavaLarvaAttachChanged)} LarvaId: {LarvaId}, VehicleId: {VehicleId}, AttachPoint: {AttachPoint}, Attached: {Attached}]";
    }
}
