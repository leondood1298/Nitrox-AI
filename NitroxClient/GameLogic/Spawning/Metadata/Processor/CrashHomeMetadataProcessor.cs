using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class CrashHomeMetadataProcessor : EntityMetadataProcessor<CrashHomeMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, CrashHomeMetadata metadata)
    {
        if (!gameObject.TryGetComponent(out CrashHome crashHome))
        {
            Log.Error($"[{nameof(CrashHomeMetadataProcessor)}] Could not find {nameof(CrashHome)} on {gameObject}");
            return;
        }

        // A negative time means the home has its crashfish; it is paired with it when the fish is here (CrashHomes)
        crashHome.spawnTime = metadata.SpawnTime;

        if (gameObject.TryGetNitroxId(out NitroxId homeId))
        {
            Resolve<CrashHomes>().Apply(crashHome, homeId, metadata.SpawnedCrashId);
        }
    }
}
