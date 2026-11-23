using System.Linq;
using Nitrox.Model.DataStructures;
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
        string? refusal = GetDockRefusal(camera.WorldCamera != null, room != null, roomWasKnown, packet.DockingIndex);
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
        if (simulationOwnershipData.TryGetLock(worldCamera.Id, out SimulationOwnershipData.PlayerLock playerLock) && playerLock.LockType == SimulationLockType.EXCLUSIVE &&
            !IsController(worldCamera.Id, playerLock.Player.SessionId))
        {
            if (TryCreateControlRelease(worldCamera.Id, playerLock, "dock", out MapRoomCameraControl? release))
            {
                await SendToAllAsync(release);
            }
            if (EndControlLock(worldCamera.Id, playerLock.Player) is { } keptSimulation)
            {
                await SendToAllAsync(keptSimulation);
            }
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
    ///     Why a camera cannot go into a dock, as far as that can be told without looking at who asks. Only a camera
    ///     out in the world can: a camera in an inventory is no world entity (a dock report can cross the pickup on
    ///     its way to the server), and the id of any other kind of entity is never a camera, whoever reports it.
    /// </summary>
    internal static string? GetDockRefusal(bool isWorldCamera, bool roomExists, bool roomWasKnown, int dockingIndex) =>
        dockingIndex is < 0 or >= MapRoomCameraMetadata.DOCK_COUNT ? "invalid_slot" :
        !roomExists ? roomWasKnown ? "room_removed" : "unknown_room" :
        !isWorldCamera ? "unknown_camera" : null;

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
    ///     either. <see cref="EntityRegistry.ReparentEntity(Entity, Entity)" /> would make it relative to the transform
    ///     of the room entity, which is empty and cannot be inverted, and leave the camera without a position.
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
