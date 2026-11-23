using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

public class SealedDoorMetadataExtractor : EntityMetadataExtractor<Sealed, EntityMetadata>
{
    public override EntityMetadata Extract(Sealed entity)
    {
        // A door that has to be cut open is one entity with one metadata slot, which the door's metadata fills
        if (entity.TryGetComponent(out StarshipDoor door))
        {
            return Resolve<StarshipDoorMetadataExtractor>().Extract(door);
        }

        return new SealedDoorMetadata(entity._sealed, entity.openedAmount);
    }
}
