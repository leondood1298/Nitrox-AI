using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class StayAtLeashPositionMetadataProcessor : EntityMetadataProcessor<StayAtLeashPositionMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, StayAtLeashPositionMetadata metadata)
    {
        // Before Creature.InitializeOnce, whose infection roll must see that the infection is decided
        Resolve<CreatureInfections>().ApplyReceived(gameObject, metadata);
        Resolve<CreatureLeashes>().ApplyReceived(gameObject, metadata);
    }
}
