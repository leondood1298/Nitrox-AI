using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

public class DiveReelMetadataExtractor : EntityMetadataExtractor<DiveReel, DiveReelMetadata>
{
    public override DiveReelMetadata Extract(DiveReel diveReel)
    {
        List<NitroxVector3> positions = diveReel.nodePositions == null ? [] : diveReel.nodePositions.Select(position => position.ToDto()).ToList();
        return new DiveReelMetadata(positions);
    }
}
