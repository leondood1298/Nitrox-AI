using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class MapRoomMetadataProcessor(MapRoomScans mapRoomScans) : EntityMetadataProcessor<MapRoomMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, MapRoomMetadata metadata)
    {
        if (!gameObject.TryGetComponent(out MapRoomFunctionality mapRoom))
        {
            Log.Error($"Could not find MapRoomFunctionality on {gameObject.name}");
            return;
        }
        mapRoomScans.ApplyMetadata(mapRoom, metadata);
    }
}
