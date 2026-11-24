using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class LavaLarvaMetadataProcessor : EntityMetadataProcessor<LavaLarvaMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, LavaLarvaMetadata metadata)
    {
        // Before InitializeOnce, whose infection roll must see that the infection is decided
        Resolve<CreatureInfections>().ApplyReceived(gameObject, metadata);
        Resolve<CreatureLeashes>().ApplyReceived(gameObject, metadata);
        Resolve<LavaLarvae>().ApplyStoredState(gameObject, metadata);
    }
}
