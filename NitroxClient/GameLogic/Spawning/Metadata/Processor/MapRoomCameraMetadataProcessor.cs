using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

/// <summary>
///     Applies what the server holds for a Scanner Room camera: when the camera spawns (in a dock, in the world or in an
///     inventory), when another player changed its light, battery or health, when the server gives it its number, and
///     when the server answers something it refused from this client.
/// </summary>
public class MapRoomCameraMetadataProcessor(MapRoomCameras mapRoomCameras, MapRoomCameraDocks mapRoomCameraDocks, MapRoomCameraStates mapRoomCameraStates,
    MapRoomCameraPreviews mapRoomCameraPreviews) : EntityMetadataProcessor<MapRoomCameraMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, MapRoomCameraMetadata metadata)
    {
        if (!gameObject.TryGetComponent(out MapRoomCamera camera) || !gameObject.TryGetNitroxId(out NitroxId cameraId))
        {
            Log.Warn($"[{nameof(MapRoomCameraMetadataProcessor)}] {gameObject.name} has camera state but is no camera with an id");
            return;
        }

        // Before MapRoomCamera.Start, which gives a camera without a number its place in this client's own list.
        if (metadata.CameraNumber > 0 && MapRoomCameraStates.SetCameraNumber(camera, metadata.CameraNumber))
        {
            // The screens show a camera's number with its picture.
            mapRoomCameraPreviews.ShowPending();
        }
        mapRoomCameraDocks.ApplyDockingIndex(camera, metadata.DockingIndex);
        mapRoomCameraStates.Apply(camera, cameraId, metadata);
        mapRoomCameras.CameraSpawned(cameraId);
        // A picture that arrived before its camera had spawned can be shown now.
        mapRoomCameraPreviews.ShowPending();
    }
}
