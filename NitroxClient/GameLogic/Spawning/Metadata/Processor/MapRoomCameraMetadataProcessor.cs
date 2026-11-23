using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

/// <summary>
///     Applies what the server holds for a Scanner Room camera: when the camera spawns (in a dock, in the world or in an
///     inventory), when the server gives it its number, and when the server answers a dock or undock it refused.
/// </summary>
public class MapRoomCameraMetadataProcessor(MapRoomCameras mapRoomCameras, MapRoomCameraDocks mapRoomCameraDocks) : EntityMetadataProcessor<MapRoomCameraMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, MapRoomCameraMetadata metadata)
    {
        if (!gameObject.TryGetComponent(out MapRoomCamera camera) || !gameObject.TryGetNitroxId(out NitroxId cameraId))
        {
            Log.Warn($"[{nameof(MapRoomCameraMetadataProcessor)}] {gameObject.name} has camera state but is no camera with an id");
            return;
        }

        // Before MapRoomCamera.Start, which gives a camera without a number its place in this client's own list.
        if (metadata.CameraNumber > 0)
        {
            mapRoomCameras.SetCameraNumber(camera, metadata.CameraNumber);
        }
        mapRoomCameraDocks.ApplyDockingIndex(camera, metadata.DockingIndex);
        // The same paths that apply live updates: they wait until the camera's battery exists and ignore anything
        // older than what this client already knows.
        mapRoomCameras.ProcessComponentState(new MapRoomCameraComponentState(cameraId, metadata.Energy, metadata.Health, metadata.ComponentRevision, true, true));
        mapRoomCameras.ProcessLight(new MapRoomCameraLight(cameraId, metadata.LightOn, metadata.LightRevision, true, true));
        mapRoomCameras.CameraSpawned(cameraId);
    }
}
