using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

public class FireMetadataExtractor : EntityMetadataExtractor<Fire, FireMetadata>
{
    public override FireMetadata Extract(Fire fire)
    {
        return new FireMetadata(fire.livemixin.health);
    }
}
