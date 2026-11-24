using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

[Serializable]
[DataContract]
public class PlantableMetadata : EntityMetadata
{
    /// <summary>
    ///     <see cref="Health" /> of a plant that has not grown yet, or whose health was never reported.
    /// </summary>
    public const float NO_HEALTH = -1f;

    [DataMember(Order = 1)]
    public float TimeStartGrowth { get; set; }

    [DataMember(Order = 2)]
    public int SlotID { get; set; }

    // TODO: When the metadata system is reworked and we can have multiple metadatas on one entity, this won't be required anymore
    [DataMember(Order = 3)]
    public FruitPlantMetadata FruitPlantMetadata { get; set; }

    /// <summary>
    ///     Health of the grown plant (a knife takes it away), or <see cref="NO_HEALTH" />.
    /// </summary>
    [DataMember(Order = 4)]
    public float Health { get; set; } = NO_HEALTH;

    [IgnoreConstructor]
    protected PlantableMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public PlantableMetadata(float timeStartGrowth, int slotID)
    {
        TimeStartGrowth = timeStartGrowth;
        SlotID = slotID;
    }

    public PlantableMetadata(float timeStartGrowth, int slotID, FruitPlantMetadata fruitPlantMetadata, float health)
    {
        TimeStartGrowth = timeStartGrowth;
        SlotID = slotID;
        FruitPlantMetadata = fruitPlantMetadata;
        Health = health;
    }

    public override string ToString()
    {
        return $"[{nameof(PlantableMetadata)} TimeStartGrowth: {TimeStartGrowth}, SlotID: {SlotID}, FruitPlantMetadata: {FruitPlantMetadata}, Health: {Health}]";
    }
}
