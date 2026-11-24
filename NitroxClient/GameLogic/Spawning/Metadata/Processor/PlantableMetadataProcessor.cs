using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class PlantableMetadataProcessor(FruitPlantMetadataProcessor fruitPlantMetadataProcessor) : EntityMetadataProcessor<PlantableMetadata>
{
    private readonly FruitPlantMetadataProcessor fruitPlantMetadataProcessor = fruitPlantMetadataProcessor;

    public override void ProcessMetadata(GameObject gameObject, PlantableMetadata metadata)
    {
        if (!gameObject.TryGetComponent(out Plantable plantable))
        {
            Log.Error($"[{nameof(PlantableMetadataProcessor)}] Could not find {nameof(Plantable)} on {gameObject.name}");
            return;
        }

        // For Plantables which were replaced by the GrowingPlant object
        if (plantable.growingPlant)
        {
            plantable.growingPlant.timeStartGrowth = metadata.TimeStartGrowth;
        }
        // For regular Plantables
        else if (plantable.model.TryGetComponent(out GrowingPlant growingPlant))
        {
            // Calculation from GrowingPlant.GetProgress
            if (metadata.TimeStartGrowth == -1f)
            {
                plantable.plantAge = 0;
            }
            else
            {
                // This is the reversed calculation because we're looking for "progress" while we already know timeStartGrowth
                plantable.plantAge = Mathf.Clamp((DayNightCycle.main.timePassedAsFloat - metadata.TimeStartGrowth) / growingPlant.GetGrowthDuration(), 0f, growingPlant.maxProgress);
            }
        }

        ApplyToGrownPlant(plantable, metadata);
    }

    /// <summary>
    ///     The fruit and health of the grown plant. Also called once the plant has grown (see
    ///     GrowingPlant_SpawnGrownModelAsync_Patch), as the grown plant is made some frames after the seed is planted.
    /// </summary>
    public void ApplyToGrownPlant(Plantable plantable, PlantableMetadata metadata)
    {
        // TODO: Refer to the TODO in PlantableMetadata
        if (metadata.FruitPlantMetadata != null)
        {
            fruitPlantMetadataProcessor.ProcessMetadata(plantable.gameObject, metadata.FruitPlantMetadata);
        }

        // A health of zero is never applied: the plant's death is its removal (GrownPlant_OnKill_Patch)
        if (metadata.Health > 0f && plantable.linkedGrownPlant && plantable.linkedGrownPlant.TryGetComponent(out LiveMixin liveMixin) && liveMixin.IsAlive())
        {
            Resolve<LiveMixinManager>().SyncRemoteHealth(liveMixin, Mathf.Min(metadata.Health, liveMixin.maxHealth));
        }
    }
}
