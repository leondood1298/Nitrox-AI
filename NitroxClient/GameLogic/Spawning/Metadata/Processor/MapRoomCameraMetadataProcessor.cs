using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Extensions;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

/// <summary>
///     Applies the saved light, battery and health of a Scanner Room camera that belongs to no Scanner Room when it
///     spawns. A registered camera gets these from its room instead.
/// </summary>
public class MapRoomCameraMetadataProcessor(MapRoomCameras mapRoomCameras) : EntityMetadataProcessor<MapRoomCameraMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, MapRoomCameraMetadata metadata)
    {
        if (!gameObject.TryGetNitroxId(out NitroxId cameraId))
        {
            Log.Warn($"[{nameof(MapRoomCameraMetadataProcessor)}] {gameObject.name} has camera state but no id");
            return;
        }

        // The same paths that apply live updates: they wait until the camera's battery exists and ignore anything
        // older than what this client already knows.
        mapRoomCameras.ProcessComponentState(new MapRoomCameraComponentState(cameraId, metadata.Energy, metadata.Health, metadata.ComponentRevision, true, true));
        mapRoomCameras.ProcessLight(new MapRoomCameraLight(cameraId, metadata.LightOn, metadata.LightRevision, true, true));
    }
}
