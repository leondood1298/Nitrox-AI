using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class FireMetadataProcessor : EntityMetadataProcessor<FireMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, FireMetadata metadata)
    {
        // The fire's entity is the object above the Fire (an Aurora fire, or a Cyclops fire that already burned); a
        // Cyclops fire that is spawned anew gives the Fire's own object
        Fire fire = gameObject.GetComponentInChildren<Fire>(true);
        if (!fire)
        {
            Log.Error($"[{nameof(FireMetadataProcessor)}] Could not find a Fire on {gameObject.name}");
            return;
        }

        Resolve<Fires>().RestoreHealth(fire, metadata.Health);
    }
}
