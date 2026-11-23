using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class CreatureInfectionMetadataProcessor : EntityMetadataProcessor<CreatureInfectionMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, CreatureInfectionMetadata metadata)
    {
        Resolve<CreatureInfections>().ApplyReceived(gameObject, metadata);
    }
}
