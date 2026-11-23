using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

public class StarshipDoorMetadataExtractor : EntityMetadataExtractor<StarshipDoor, StarshipDoorMetadata>
{
    public override StarshipDoorMetadata Extract(StarshipDoor door)
    {
        // A door that has to be cut open has its Sealed on the same object (StarshipDoor.Start takes it from there)
        if (door.TryGetComponent(out Sealed sealedDoor))
        {
            return new(door.doorLocked, door.doorOpen, sealedDoor._sealed, sealedDoor.openedAmount);
        }

        return new(door.doorLocked, door.doorOpen, false, 0);
    }
}
