using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Extensions;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;
using NitroxClient.MonoBehaviours;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

public class MapRoomMetadataExtractor : EntityMetadataExtractor<MapRoomFunctionality, MapRoomMetadata>
{
	public override MapRoomMetadata Extract(MapRoomFunctionality entity)
	{
		MapRoomNetworkState state = entity.GetComponent<MapRoomNetworkState>();
		long generation = state ? state.Generation : 0;
		long revision = state ? state.Revision : 0;
		return new MapRoomMetadata(entity.typeToScan.ToDto(), entity.numNodesScanned, generation, revision);
	}
}


