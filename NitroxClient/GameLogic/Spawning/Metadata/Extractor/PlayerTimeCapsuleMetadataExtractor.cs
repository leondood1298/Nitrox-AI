using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

public class PlayerTimeCapsuleMetadataExtractor : EntityMetadataExtractor<PlayerTimeCapsule, PlayerTimeCapsuleMetadata>
{
    public override PlayerTimeCapsuleMetadata Extract(PlayerTimeCapsule capsule)
    {
        // The picture is left out: it is a screenshot on this player's computer
        return new PlayerTimeCapsuleMetadata(capsule.text, capsule.title);
    }
}
