using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class StalkerMetadataProcessor : EntityMetadataProcessor<StalkerMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, StalkerMetadata metadata)
    {
        // Before InitializeOnce, whose infection roll must see that the infection is decided
        Resolve<CreatureInfections>().ApplyReceived(gameObject, metadata.InfectedAmount);

        if (gameObject.TryGetComponent(out Stalker stalker))
        {
            Resolve<StalkerFriendships>().ApplyState(stalker, metadata);
        }
        else
        {
            Log.Error($"Could not find {nameof(Stalker)} on {gameObject.name}");
        }
    }
}
