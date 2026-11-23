using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class SealedDoorMetadataProcessor : EntityMetadataProcessor<SealedDoorMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, SealedDoorMetadata metadata)
    {
        Log.Info($"Received door metadata change for {gameObject.name} with data of {metadata}");

        ApplySealedState(gameObject.GetComponent<Sealed>(), metadata.Sealed, metadata.OpenedAmount);
    }

    /// <summary>
    ///     Applies the cut state of an object that has to be laser cut open, alone or as part of a door's metadata.
    /// </summary>
    public static void ApplySealedState(Sealed sealedObject, bool isSealed, float openedAmount)
    {
        sealedObject._sealed = isSealed;
        sealedObject.openedAmount = openedAmount;

        LaserCutObject laseredObject = sealedObject.GetComponent<LaserCutObject>();

        if (laseredObject && isSealed)
        {
            laseredObject.lastCutValue = openedAmount;
            laseredObject.ActivateFX();
        }
    }
}
