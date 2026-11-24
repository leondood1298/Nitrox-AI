using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class WarpedInCreatureMetadataProcessor : EntityMetadataProcessor<WarpedInCreatureMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, WarpedInCreatureMetadata metadata)
    {
        // Before InitializeOnce, whose infection roll must see that the infection is decided
        Resolve<CreatureInfections>().ApplyReceived(gameObject, metadata);
        Resolve<CreatureLeashes>().ApplyReceived(gameObject, metadata);
        Resolve<SharedCreatureSpawns>().ApplyWarpOutTime(gameObject, metadata.WarpOutTime);
    }
}
