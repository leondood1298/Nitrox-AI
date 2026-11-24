using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     Whether a Lava Larva is attached to a vehicle or Cyclops (AttachToVehicle), to which and at which point, so that a
///     player who loads the larva or the vehicle later sees it there. Sent by the game that had the larva attach or come off.
/// </summary>
[Serializable, DataContract]
public class LavaLarvaMetadata : CreatureMetadata
{
    [DataMember(Order = 1)]
    public bool Attached { get; }

    /// <summary>The vehicle or Cyclops, when <see cref="Attached" />.</summary>
    [DataMember(Order = 2)]
    public NitroxId VehicleId { get; }

    /// <summary>The index of the point in the vehicle's LavaLarvaTarget.attachPoints, when <see cref="Attached" />.</summary>
    [DataMember(Order = 3)]
    public int AttachPoint { get; }

    [IgnoreConstructor]
    protected LavaLarvaMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public LavaLarvaMetadata(bool attached, NitroxId vehicleId, int attachPoint, float infectedAmount, float enzymeEndTime = NO_ENZYME, bool hasLeash = false,
                             NitroxVector3 leashPosition = default, bool hasSize = false, float size = 0f) : base(infectedAmount, enzymeEndTime, hasLeash, leashPosition, hasSize, size)
    {
        Attached = attached;
        VehicleId = vehicleId;
        AttachPoint = attachPoint;
    }

    /// <summary>
    ///     The same larva, come off, with everything else it carries.
    /// </summary>
    public LavaLarvaMetadata Detached()
    {
        return new LavaLarvaMetadata(false, null, -1, InfectedAmount, EnzymeEndTime, HasLeash, LeashPosition, HasSize, Size);
    }

    public override string ToString()
    {
        return $"[{nameof(LavaLarvaMetadata)} Attached: {Attached}, VehicleId: {VehicleId}, AttachPoint: {AttachPoint}, {base.ToString()}]";
    }
}
