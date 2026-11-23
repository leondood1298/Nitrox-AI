using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

/// <summary>
///     Lets a creature's decided infection and enzyme travel with it when it becomes an item and comes back out (picked up,
///     dropped, into and out of containers and containment), wherever its entity is made again from what is extracted.
/// </summary>
public class CreatureInfectionMetadataExtractor : EntityMetadataExtractor<InfectedMixin, CreatureInfectionMetadata>
{
    public override CreatureInfectionMetadata Extract(InfectedMixin infectedMixin)
    {
        return Resolve<CreatureInfections>().GetDecidedState(infectedMixin);
    }
}
