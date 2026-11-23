using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using Nitrox.Model.DataStructures.Unity;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

[Serializable, DataContract]
public class StayAtLeashPositionMetadata : CreatureMetadata
{
    [DataMember(Order = 1)]
    public NitroxVector3 LeashPosition { get; }

    [IgnoreConstructor]
    protected StayAtLeashPositionMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public StayAtLeashPositionMetadata(NitroxVector3 leashPosition, float infectedAmount, float enzymeEndTime = NO_ENZYME) : base(infectedAmount, enzymeEndTime)
    {
        LeashPosition = leashPosition;
    }

    public override string ToString()
    {
        return $"[{nameof(StayAtLeashPositionMetadata)} LeashPosition: {LeashPosition}, {base.ToString()}]";
    }
}
