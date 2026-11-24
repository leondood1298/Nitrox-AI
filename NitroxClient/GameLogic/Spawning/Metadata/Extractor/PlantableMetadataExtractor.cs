using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

public class PlantableMetadataExtractor(FruitPlantMetadataExtractor fruitPlantMetadataExtractor) : EntityMetadataExtractor<Plantable, PlantableMetadata>
{
    private readonly FruitPlantMetadataExtractor fruitPlantMetadataExtractor = fruitPlantMetadataExtractor;

    public override PlantableMetadata Extract(Plantable plantable)
    {
        return Extract(plantable, null);
    }

    /// <param name="changedFruitPlant">
    ///     The plant whose fruit just changed (a fruit picked or grown), which the fruit is taken from. A report then
    ///     describes the plant the player picked from or saw grow, also should the seed's grown plant
    ///     (<see cref="Plantable.linkedGrownPlant" />) be another object (see PlantFruitDiagnostics).
    /// </param>
    public PlantableMetadata Extract(Plantable plantable, FruitPlant changedFruitPlant)
    {
        // An entity has one metadata slot: a plantable food that is carried keeps its decay there (EatableMetadata)
        if (!HasStateToKeep(plantable.currentPlanter))
        {
            return null;
        }

        // Default value for no progress is -1
        PlantableMetadata metadata = new(plantable.growingPlant ? plantable.growingPlant.timeStartGrowth : -1, plantable.GetSlotID());

        // TODO: Refer to the TODO in PlantableMetadata
        if (changedFruitPlant)
        {
            metadata.FruitPlantMetadata = fruitPlantMetadataExtractor.Extract(changedFruitPlant);
        }
        if (plantable.linkedGrownPlant)
        {
            if (!changedFruitPlant && plantable.linkedGrownPlant.TryGetComponent(out FruitPlant fruitPlant))
            {
                metadata.FruitPlantMetadata = fruitPlantMetadataExtractor.Extract(fruitPlant);
            }
            if (plantable.linkedGrownPlant.TryGetComponent(out LiveMixin liveMixin) && liveMixin.health > 0f)
            {
                metadata.Health = liveMixin.health;
            }
        }

        return metadata;
    }

    /// <summary>
    ///     Only a plant in a planter has growth, fruit and health to keep. Out of one, the game keeps nothing of it either
    ///     (Plantable.OnProtoSerialize stores the age only while planted).
    /// </summary>
    public static bool HasStateToKeep(bool isInPlanter) => isInPlanter;
}
