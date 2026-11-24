using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class SandSharkMetadataProcessor : EntityMetadataProcessor<SandSharkMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, SandSharkMetadata metadata)
    {
        // Before InitializeOnce, whose infection roll must see that the infection is decided
        Resolve<CreatureInfections>().ApplyReceived(gameObject, metadata.InfectedAmount);
        Resolve<CreatureLeashes>().ApplyReceived(gameObject, metadata);

        if (gameObject.TryGetComponent(out BurryInSand burryInSand))
        {
            Resolve<SandSharkBurrows>().ApplyState(burryInSand, metadata.Buried, metadata.BurryPosition.ToUnity(), metadata.FloorAlign.ToUnity());
        }
        else
        {
            Log.Error($"Could not find {nameof(BurryInSand)} on {gameObject.name}");
        }
    }
}
