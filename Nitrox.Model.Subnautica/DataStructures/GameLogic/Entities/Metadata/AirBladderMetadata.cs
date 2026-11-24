using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     The air in an air bladder (AirBladder.oxygen): full once it was filled above water, emptied while it lifts its
///     player. The other players see the bladder as full as it is.
/// </summary>
[Serializable]
[DataContract]
public class AirBladderMetadata : EntityMetadata
{
    [DataMember(Order = 1)]
    public float Oxygen { get; }

    [IgnoreConstructor]
    protected AirBladderMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public AirBladderMetadata(float oxygen)
    {
        Oxygen = oxygen;
    }

    public override string ToString()
    {
        return $"[{nameof(AirBladderMetadata)} Oxygen: {Oxygen}]";
    }
}
