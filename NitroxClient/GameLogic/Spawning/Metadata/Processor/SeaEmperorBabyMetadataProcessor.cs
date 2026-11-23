using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class SeaEmperorBabyMetadataProcessor : EntityMetadataProcessor<SeaEmperorBabyMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, SeaEmperorBabyMetadata metadata)
    {
        Resolve<SeaEmperorBabies>().Apply(gameObject, metadata);
    }
}
