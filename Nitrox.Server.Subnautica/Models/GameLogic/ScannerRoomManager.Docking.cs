using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Which camera sits in which dock. A docked camera is a child entity of its room, as a docked vehicle is a child
///     of its moonpool: the entity tree is what is saved and what a joining player is sent. The dock within the room
///     is the <see cref="MapRoomCameraMetadata.DockingIndex" /> of the camera.
/// </summary>
internal sealed partial class ScannerRoomManager
{
    /// <summary>
    ///     Handles the report of a camera's simulating player that the camera went into a dock: the camera becomes a
    ///     child of the room, and the other players are told. A report that is refused is dropped; its sender has
    ///     docked the camera in its own game and is sent the camera's real state to undo that.
    /// </summary>
    /// <returns>Whether the camera sits in that dock now.</returns>
    public async Task<bool> DockCameraAsync(Player sender, MapRoomCameraDocked packet)
    {
        using Hold held = await EnterAsync();
        CameraView camera = GetCamera(packet.CameraId);
        bool roomWasKnown = packet.MapRoomId != null && roomsById.ContainsKey(packet.MapRoomId);
        TryGetRoom(packet.MapRoomId, out MapRoomEntity? room);
        // Only a camera out in the world can go into a dock: a camera in an inventory is no world entity (a dock
        // report can cross the pickup on its way to the server), and the id of any other kind of entity is never a
        // camera, whoever reports it.
        string? refusal = packet.DockingIndex is < 0 or >= MapRoomCameraMetadata.DOCK_COUNT ? "invalid_slot" :
                          room == null ? roomWasKnown ? "room_removed" : "unknown_room" :
                          camera.WorldCamera == null ? "unknown_camera" : null;
        if (refusal == null && camera.DockedRoom == room && camera.State.DockingIndex == packet.DockingIndex)
        {
            diagnostics.RecordAccepted("dock", room, packet.CameraId, sender.SessionId, packet.DockingIndex, "idempotent");
            return true;
        }
        refusal ??= simulationOwnershipData.GetPlayerForLock(packet.CameraId) != sender ? "non_owner" :
                    camera.DockedRoom != null || GetDockedCamera(room!, packet.DockingIndex) != null ? "slot_conflict" : null;
        if (refusal != null || room == null || camera.WorldCamera is not { } worldCamera)
        {
            diagnostics.RecordRejected("dock", room, packet.CameraId, sender.SessionId, packet.DockingIndex, refusal);
            await CorrectDockingAsync(sender, camera);
            return false;
        }

        knownCameraIds.TryAdd(worldCamera.Id, 0);
        SetParent(worldCamera, room);
        SetState(worldCamera, EnsureCameraNumber(camera.State).WithDockingIndex(packet.DockingIndex));
        diagnostics.RecordAccepted("dock", room, worldCamera.Id, sender.SessionId, packet.DockingIndex, "changed");

        // The player who flew the camera in keeps control while it is docked, as in the game. Any other exclusive
        // lock was taken for something that ended with the dock (a Stalker carrying the camera); its holder goes
        // on simulating the camera like any other entity.
        if (simulationOwnershipData.TryGetLock(worldCamera.Id, out SimulationOwnershipData.PlayerLock playerLock) && !IsController(worldCamera.Id, playerLock.Player.SessionId))
        {
            await GiveUpExclusiveLockAsync(worldCamera.Id, playerLock.Player);
        }
        await SendToOthersAsync(sender, packet);
        return true;
    }

    /// <summary>
    ///     Handles the report of a camera's simulating player that the camera left its dock: the camera becomes an
    ///     entity of the world again, and the other players are told. A report that is refused is dropped and its
    ///     sender is sent the camera's real state.
    /// </summary>
    /// <returns>Whether the camera is out of that room's docks now.</returns>
    public async Task<bool> UndockCameraAsync(Player sender, MapRoomCameraUndocked packet)
    {
        using Hold held = await EnterAsync();
        CameraView camera = GetCamera(packet.CameraId);
        TryGetRoom(packet.MapRoomId, out MapRoomEntity? room);
        if (camera.WorldCamera is { } looseCamera && camera.DockedRoom == null)
        {
            diagnostics.RecordAccepted("undock", room, looseCamera.Id, sender.SessionId, reason: "idempotent");
            return true;
        }
        string? refusal = camera.WorldCamera == null ? "unknown_camera" :
                          room == null || camera.DockedRoom != room ? "not_docked_there" :
                          simulationOwnershipData.GetPlayerForLock(packet.CameraId) != sender ? "non_owner" : null;
        if (refusal != null || camera.WorldCamera is not { } worldCamera)
        {
            diagnostics.RecordRejected("undock", room, packet.CameraId, sender.SessionId, camera.DockedSlot, refusal);
            await CorrectDockingAsync(sender, camera);
            return false;
        }

        Undock(worldCamera, camera.State);
        diagnostics.RecordAccepted("undock", room, worldCamera.Id, sender.SessionId, camera.DockedSlot, "changed");
        await SendToOthersAsync(sender, packet);
        return true;
    }

    /// <summary>
    ///     The camera in one of the room's docks, or null.
    /// </summary>
    private static WorldEntity? GetDockedCamera(MapRoomEntity room, int dockingIndex) =>
        room.ChildEntities.OfType<WorldEntity>().FirstOrDefault(child => IsCamera(child) && ReadState(child).DockingIndex == dockingIndex);

    /// <summary>
    ///     The cameras in the room's docks.
    /// </summary>
    private static WorldEntity[] GetDockedCameras(MapRoomEntity room) =>
        room.ChildEntities.OfType<WorldEntity>().Where(child => IsCamera(child) && ReadState(child).IsDocked).ToArray();

    /// <summary>
    ///     Takes a camera out of its room and makes it an entity of the world.
    /// </summary>
    private void Undock(WorldEntity camera, MapRoomCameraMetadata state)
    {
        SetParent(camera, null);
        SetState(camera, state.WithDockingIndex(MapRoomCameraMetadata.NOT_DOCKED));
        worldEntityManager.TrackEntityInTheWorld(camera);
    }

    /// <summary>
    ///     Makes a camera a child of a room, or an entity without a parent. Only the entity tree changes: the camera's
    ///     transform stays a place in the world, as in the game, which does not put a docked camera under its room
    ///     either. The room's transform is empty, and <see cref="NitroxTransform.SetParent" /> never links a transform
    ///     to an empty one, so after a load the camera's transform is not linked to the room's either.
    /// </summary>
    private void SetParent(WorldEntity camera, MapRoomEntity? room)
    {
        if (camera.ParentId != null && entityRegistry.TryGetEntityById(camera.ParentId, out Entity? parent))
        {
            parent.ChildEntities.RemoveAll(child => child.Id == camera.Id);
        }
        camera.ParentId = room?.Id;
        room?.ChildEntities.Add(camera);
    }

    /// <summary>
    ///     Gives every camera of a loaded world whose place is missing or not a number a place again: a world saved by an
    ///     older build can hold such cameras (cameras that left their dock after a restart were stored at NaN). A camera
    ///     out in the world goes into a free dock, as if it had come home; the save does not say which room it came
    ///     from, so it is the first free dock of the rooms in the order of their ids. Without a free dock, and for a
    ///     camera that sits in a dock already, the place is that of the room's base.
    /// </summary>
    private void PlaceCamerasWithoutPlace()
    {
        WorldEntity[] lost = [.. entityRegistry.GetEntities<WorldEntity>()
                                               .Where(camera => IsCamera(camera) && (camera.Transform == null || !camera.Transform.IsFinite()))
                                               .OrderBy(camera => camera.Id.ToString(), StringComparer.Ordinal)];
        foreach (WorldEntity camera in lost)
        {
            CameraView view = GetCamera(camera.Id);
            MapRoomEntity? room = view.DockedRoom;
            string outcome = room != null ? $"stays in dock {view.DockedSlot} of Scanner Room {room.Id}" : "";
            if (room == null && TryFindFreeDock(out MapRoomEntity? freeRoom, out int dockingIndex))
            {
                SetParent(camera, freeRoom);
                SetState(camera, view.State.WithDockingIndex(dockingIndex));
                room = freeRoom;
                outcome = $"was put into dock {dockingIndex} of Scanner Room {room.Id}";
            }
            MapRoomEntity? placeRoom = room ?? GetRooms().OrderBy(candidate => candidate.Id.ToString(), StringComparer.Ordinal).FirstOrDefault();
            NitroxVector3 place = placeRoom != null ? GetBasePosition(placeRoom) : NitroxVector3.Zero;
            if (room == null)
            {
                outcome = placeRoom != null ? $"was put at the base of Scanner Room {placeRoom.Id}" : "was put at the origin of the world, as there is no Scanner Room";
            }
            camera.Transform ??= new NitroxTransform();
            camera.Transform.SetParent(null, false);
            camera.Transform.LocalPosition = place;
            camera.Transform.LocalRotation = NitroxQuaternion.Identity;
            camera.Transform.LocalScale = NitroxVector3.One;
            logger.ZLogWarning($"Scanner Room camera {camera.Id} (number {view.State.CameraNumber}) was saved without a valid place and {outcome}, at {place}");
            diagnostics.RecordAccepted("heal_place", room, camera.Id, slot: room != null ? ReadState(camera).DockingIndex : null, reason: room != null ? "docked" : "placed");
        }
    }

    /// <summary>
    ///     The first dock without a camera, in the rooms in the order of their ids.
    /// </summary>
    private bool TryFindFreeDock([NotNullWhen(true)] out MapRoomEntity? room, out int dockingIndex)
    {
        foreach (MapRoomEntity candidate in GetRooms().OrderBy(candidate => candidate.Id.ToString(), StringComparer.Ordinal))
        {
            for (int index = 0; index < MapRoomCameraMetadata.DOCK_COUNT; index++)
            {
                if (GetDockedCamera(candidate, index) == null)
                {
                    room = candidate;
                    dockingIndex = index;
                    return true;
                }
            }
        }
        room = null;
        dockingIndex = MapRoomCameraMetadata.NOT_DOCKED;
        return false;
    }

    /// <summary>
    ///     A room has no place of its own on the server (its transform is empty); its base has.
    /// </summary>
    private NitroxVector3 GetBasePosition(MapRoomEntity room) =>
        room.ParentId != null && entityRegistry.TryGetEntityById(room.ParentId, out WorldEntity? parent) && parent.Transform != null && parent.Transform.Position.IsFinite()
            ? parent.Transform.Position
            : NitroxVector3.Zero;

    /// <summary>
    ///     Sends a player whose dock or undock was refused what the server holds for the camera. Their game has
    ///     already moved the camera; with this they move it back.
    /// </summary>
    private async Task CorrectDockingAsync(Player sender, CameraView camera)
    {
        if (camera.WorldCamera is { } worldCamera)
        {
            await ReplyAsync(sender, new EntityMetadataUpdate(worldCamera.Id, camera.State));
        }
    }
}
