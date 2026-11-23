using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

/// <remarks>
///     Metadata extractors live as long as the program, the service that knows a room's scan as long as one session,
///     so it is looked up when it is needed.
/// </remarks>
public class MapRoomMetadataExtractor : EntityMetadataExtractor<MapRoomFunctionality, MapRoomMetadata>
{
    public override MapRoomMetadata Extract(MapRoomFunctionality entity) => Resolve<MapRoomScans>().GetMetadata(entity);
}
