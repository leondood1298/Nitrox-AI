using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class AirBladderMetadataProcessor : EntityMetadataProcessor<AirBladderMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, AirBladderMetadata metadata)
    {
        if (!gameObject.TryGetComponent(out AirBladder airBladder))
        {
            Log.Error($"[{nameof(AirBladderMetadataProcessor)}] Could not find {nameof(AirBladder)} on {gameObject.name}");
            return;
        }
        airBladder.oxygen = metadata.Oxygen;
        // How inflated the bladder looks, as AirBladder.UpdateInflateState sets it
        if (airBladder.animator && airBladder.oxygenCapacity > 0f)
        {
            airBladder.animator.SetFloat(AirBladder.kAnimInflate, metadata.Oxygen / airBladder.oxygenCapacity);
        }
    }
}
