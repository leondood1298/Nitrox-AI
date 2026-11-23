using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

public class MapRoomMetadataExtractor(MapRoomScans mapRoomScans) : EntityMetadataExtractor<MapRoomFunctionality, MapRoomMetadata>
{
    public override MapRoomMetadata Extract(MapRoomFunctionality entity) => mapRoomScans.GetMetadata(entity);
}
