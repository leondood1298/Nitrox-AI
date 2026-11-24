using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class FireExtinguisherHolderMetadataProcessor : EntityMetadataProcessor<FireExtinguisherHolderMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, FireExtinguisherHolderMetadata metadata)
    {
        FireExtinguisherHolder holder = gameObject.GetComponent<FireExtinguisherHolder>();

        if (holder)
        {
            holder.fuel = metadata.Fuel;
            holder.hasTank = metadata.HasExtinguisher;
            holder.tankObject.SetActive(metadata.HasExtinguisher);
            if (gameObject.TryGetNitroxId(out NitroxId holderId))
            {
                Resolve<FireExtinguisherHolders>().SetStoredTank(holderId, metadata.StoredTankId);
            }
        }
        else
        {
            Log.Error($"Could not find FireExtinguisherHolder on {gameObject.name}");
        }
    }
}
