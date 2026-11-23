using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class TimeCapsuleMetadataProcessor : EntityMetadataProcessor<TimeCapsuleMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, TimeCapsuleMetadata metadata)
    {
        if (!gameObject.TryGetComponent(out TimeCapsule capsule))
        {
            Log.Error($"[{nameof(TimeCapsuleMetadataProcessor)}] Could not find a TimeCapsule on {gameObject.name}");
            return;
        }

        TimeCapsules.ApplyKeptAnswer(capsule, metadata);
    }
}
