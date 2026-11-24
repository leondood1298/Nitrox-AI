using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using Nitrox.Model.DataStructures;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

[Serializable]
[DataContract]
public class FireExtinguisherHolderMetadata : EntityMetadata
{
    [DataMember(Order = 1)]
    public bool HasExtinguisher { get; }

    [DataMember(Order = 2)]
    public float Fuel { get; }

    /// <summary>
    ///     The extinguisher last put on the holder, null for the one it came with. The tank taken from the holder is made
    ///     new by every game that takes it; naming it after the tank that was put there tells one taking from the next.
    /// </summary>
    [DataMember(Order = 3)]
    public NitroxId? StoredTankId { get; }

    [IgnoreConstructor]
    protected FireExtinguisherHolderMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public FireExtinguisherHolderMetadata(bool hasExtinguisher, float fuel, NitroxId? storedTankId)
    {
        HasExtinguisher = hasExtinguisher;
        Fuel = fuel;
        StoredTankId = storedTankId;
    }

    public override string ToString()
    {
        return $"[FireExtinguisherHolderMetadata HasExtinguisher: {HasExtinguisher}, Fuel: {Fuel}, StoredTankId: {StoredTankId}]";
    }
}
