using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class CuteFishMetadataProcessor : EntityMetadataProcessor<CuteFishMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, CuteFishMetadata metadata)
    {
        // Before InitializeOnce, whose infection roll must see that the infection is decided
        Resolve<CreatureInfections>().ApplyReceived(gameObject, metadata);
        Resolve<CreatureLeashes>().ApplyReceived(gameObject, metadata);

        if (gameObject.TryGetComponent(out CuteFish cuteFish))
        {
            Resolve<CuteFishes>().ApplyState(cuteFish, metadata);
        }
        else
        {
            Log.Error($"Could not find {nameof(CuteFish)} on {gameObject.name}");
        }
    }
}
