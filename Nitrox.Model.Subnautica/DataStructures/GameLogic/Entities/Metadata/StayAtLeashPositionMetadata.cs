using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using Nitrox.Model.DataStructures.Unity;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     The state the server gives a creature it spawns that has no metadata of its own: where its leash is (see
///     <see cref="CreatureMetadata.LeashPosition" />), with nothing decided yet about the rest.
/// </summary>
[Serializable, DataContract]
public class StayAtLeashPositionMetadata : CreatureMetadata
{
    [IgnoreConstructor]
    protected StayAtLeashPositionMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public StayAtLeashPositionMetadata(NitroxVector3 leashPosition, float infectedAmount, float enzymeEndTime = NO_ENZYME, bool hasLeash = true, bool hasSize = false,
                                       float size = 0f) : base(infectedAmount, enzymeEndTime, hasLeash, leashPosition, hasSize, size)
    {
    }

    public override string ToString()
    {
        return $"[{nameof(StayAtLeashPositionMetadata)} {base.ToString()}]";
    }
}
