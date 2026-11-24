using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

public class WaterParkCreatureMetadataExtractor : EntityMetadataExtractor<WaterParkCreature, WaterParkCreatureMetadata>
{
    public override WaterParkCreatureMetadata Extract(WaterParkCreature entity)
    {
        bool isAlive = !entity.TryGetComponent(out LiveMixin liveMixin) || liveMixin.IsAlive();
        if (!HasGrowthToKeep(entity.age, isAlive))
        {
            return null;
        }
        CreatureInfections creatureInfections = Resolve<CreatureInfections>();
        return new(entity.age, entity.matureTime, entity.timeNextBreed, entity.bornInside, creatureInfections.GetDecidedAmount(entity), creatureInfections.GetDecidedEnzymeEndTime(entity));
    }

    /// <summary>
    ///     A fish with default values has no growth to keep. A dead one neither: the game lists WaterParkCreature before
    ///     Eatable on every fish, and an entity has one metadata slot, so its decay (EatableMetadata) has to take it, or the
    ///     dead fish came back alive and fresh for the others and after a restart.
    /// </summary>
    public static bool HasGrowthToKeep(float age, bool isAlive) => age != -1 && isAlive;
}
