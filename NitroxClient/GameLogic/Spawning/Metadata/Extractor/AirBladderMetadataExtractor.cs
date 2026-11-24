using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

public class AirBladderMetadataExtractor : EntityMetadataExtractor<AirBladder, AirBladderMetadata>
{
    public override AirBladderMetadata Extract(AirBladder airBladder)
    {
        return new AirBladderMetadata(airBladder.oxygen);
    }
}
