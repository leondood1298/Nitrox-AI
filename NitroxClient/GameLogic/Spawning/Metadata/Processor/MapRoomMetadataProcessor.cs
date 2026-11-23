using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

/// <remarks>
///     The service is looked up when it is needed, see <see cref="MapRoomCameraMetadataProcessor" />.
/// </remarks>
public class MapRoomMetadataProcessor : EntityMetadataProcessor<MapRoomMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, MapRoomMetadata metadata)
    {
        if (!gameObject.TryGetComponent(out MapRoomFunctionality mapRoom))
        {
            Log.Error($"Could not find MapRoomFunctionality on {gameObject.name}");
            return;
        }
        Resolve<MapRoomScans>().ApplyMetadata(mapRoom, metadata);
    }
}
