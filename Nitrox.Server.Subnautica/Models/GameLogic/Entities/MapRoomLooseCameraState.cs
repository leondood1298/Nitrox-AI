using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <summary>
///     Authoritative light, battery and health for Scanner Room cameras that no Scanner Room has a record for. The
///     state lives on the camera's own world entity as <see cref="MapRoomCameraMetadata" />, so it is saved and sent
///     to joining players with the camera.
/// </summary>
internal sealed class MapRoomLooseCameraState(EntityRegistry entityRegistry)
{
    private static readonly NitroxTechType mapRoomCameraTechType = new("MapRoomCamera");

    public bool TryGetCamera(NitroxId cameraId, out WorldEntity camera) =>
        entityRegistry.TryGetEntityById(cameraId, out camera) && mapRoomCameraTechType.Equals(camera.TechType);

    /// <summary>
    ///     Stores a new light state. The revision only advances when the light actually changes.
    /// </summary>
    public MapRoomCameraMetadata SetLight(WorldEntity camera, bool lightOn, out bool changed)
    {
        lock (camera)
        {
            MapRoomCameraMetadata current = Read(camera);
            changed = current.LightOn != lightOn;
            if (changed)
            {
                current = new MapRoomCameraMetadata(lightOn, current.LightRevision + 1, current.Energy, current.Health, current.ComponentRevision);
                camera.Metadata = current;
            }
            return current;
        }
    }

    /// <summary>
    ///     Stores new battery and health values. The revision only advances when a value actually changes.
    /// </summary>
    public MapRoomCameraMetadata SetComponents(WorldEntity camera, float energy, float health, out MapRoomCameraMetadata previous)
    {
        lock (camera)
        {
            previous = Read(camera);
            if (previous.Energy == energy && previous.Health == health)
            {
                return previous;
            }
            MapRoomCameraMetadata updated = new(previous.LightOn, previous.LightRevision, energy, health, previous.ComponentRevision + 1);
            camera.Metadata = updated;
            return updated;
        }
    }

    /// <summary>
    ///     Called when a camera gets its first record in a room. The record takes over the camera's state, keeping the
    ///     revisions so clients that already saw them accept what follows.
    /// </summary>
    public void MoveIntoRecord(NitroxId cameraId, MapRoomCameraRecord record)
    {
        if (!TryGetCamera(cameraId, out WorldEntity camera))
        {
            return;
        }
        lock (camera)
        {
            if (camera.Metadata is not MapRoomCameraMetadata loose)
            {
                return;
            }
            lock (record)
            {
                record.LightOn = loose.LightOn;
                record.LightRevision = loose.LightRevision;
                record.Energy = loose.Energy;
                record.Health = loose.Health;
                record.ComponentRevision = loose.ComponentRevision;
            }
            camera.Metadata = null;
        }
    }

    /// <summary>
    ///     Called when a room is removed while one of its cameras is still out in the world. The camera keeps the
    ///     state its record held.
    /// </summary>
    public void MoveOutOfRecord(MapRoomCameraRecord record)
    {
        if (!TryGetCamera(record.CameraId, out WorldEntity camera))
        {
            return;
        }
        lock (camera)
        {
            lock (record)
            {
                camera.Metadata = new MapRoomCameraMetadata(record.LightOn, record.LightRevision, record.Energy, record.Health, record.ComponentRevision);
            }
        }
    }

    private static MapRoomCameraMetadata Read(WorldEntity camera) => camera.Metadata as MapRoomCameraMetadata ?? new MapRoomCameraMetadata();
}
