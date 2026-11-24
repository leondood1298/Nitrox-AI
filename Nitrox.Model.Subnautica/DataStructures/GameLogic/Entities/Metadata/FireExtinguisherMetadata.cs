using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     The fuel left in a fire extinguisher (FireExtinguisher.fuel), spent while it sprays. An extinguisher on a wall holder
///     is no item of its own: the holder keeps its fuel (<see cref="FireExtinguisherHolderMetadata" />).
/// </summary>
[Serializable]
[DataContract]
public class FireExtinguisherMetadata : EntityMetadata
{
    [DataMember(Order = 1)]
    public float Fuel { get; }

    [IgnoreConstructor]
    protected FireExtinguisherMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public FireExtinguisherMetadata(float fuel)
    {
        Fuel = fuel;
    }

    public override string ToString()
    {
        return $"[{nameof(FireExtinguisherMetadata)} Fuel: {Fuel}]";
    }
}
