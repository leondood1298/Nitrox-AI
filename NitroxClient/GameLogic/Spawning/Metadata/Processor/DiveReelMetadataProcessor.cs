using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class DiveReelMetadataProcessor : EntityMetadataProcessor<DiveReelMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, DiveReelMetadata metadata)
    {
        if (gameObject.TryGetComponent(out DiveReel diveReel))
        {
            Resolve<DiveReels>().ShowTrail(diveReel, metadata);
        }
        else
        {
            Log.Error($"[{nameof(DiveReelMetadataProcessor)}] Could not find {nameof(DiveReel)} on {gameObject.name}");
        }
    }
}
