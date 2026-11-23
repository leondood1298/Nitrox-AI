using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

public class MapRoomCameraMetadataExtractor : EntityMetadataExtractor<MapRoomCamera, MapRoomCameraMetadata>
{
    public override MapRoomCameraMetadata Extract(MapRoomCamera entity) => MapRoomCameraStates.Extract(entity);
}
