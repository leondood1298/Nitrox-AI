using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class StarshipDoorMetadataProcessor : EntityMetadataProcessor<StarshipDoorMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, StarshipDoorMetadata metadata)
    {
        StarshipDoor starshipDoor = gameObject.GetComponent<StarshipDoor>();

        // The door patches report lock changes; applying received state must not report it back
        using (PacketSuppressor<EntityMetadataUpdate>.Suppress())
        {
            starshipDoor.doorOpen = metadata.DoorOpen;
            starshipDoor.doorLocked = metadata.DoorLocked;
            if (metadata.DoorLocked)
            {
                starshipDoor.LockDoor();
            }
            else
            {
                starshipDoor.UnlockDoor();
            }

            // A door that has to be cut open keeps its cut state in this metadata as well
            if (gameObject.TryGetComponent(out Sealed sealedDoor))
            {
                SealedDoorMetadataProcessor.ApplySealedState(sealedDoor, metadata.Sealed, metadata.OpenedAmount);
            }
        }
    }
}
