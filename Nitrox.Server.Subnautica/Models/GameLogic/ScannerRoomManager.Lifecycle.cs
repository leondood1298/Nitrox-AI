using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Helper;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Rooms and cameras coming into the world and leaving it.
/// </summary>
internal sealed partial class ScannerRoomManager
{
    /// <summary>
    ///     Called for a newly built room before it becomes reachable.
    /// </summary>
    public void RoomBuilt(MapRoomEntity room)
    {
        using Hold held = Enter();
        roomsById[room.Id] = room;
        // The builder's client made the cameras that come with the room. It cannot know which numbers are free, and
        // what else it says about them does not count: a new camera is full, intact, dark and in the next free dock.
        // A camera that comes with a room is new. One with the id of an entity that exists would take that entity's
        // place in the registry: a way to dock any camera without simulating it, or to replace something else.
        bool[] taken = new bool[MapRoomCameraMetadata.DOCK_COUNT];
        foreach (WorldEntity camera in room.ChildEntities.OfType<WorldEntity>().Where(IsCamera).ToArray())
        {
            int requested = ReadState(camera).DockingIndex;
            int dockingIndex = requested is >= 0 and < MapRoomCameraMetadata.DOCK_COUNT && !taken[requested] ? requested : Array.IndexOf(taken, false);
            if (dockingIndex < 0 || entityRegistry.TryGetEntityById(camera.Id, out Entity? _))
            {
                room.ChildEntities.Remove(camera);
                continue;
            }
            taken[dockingIndex] = true;
            camera.ParentId = room.Id;
            SetState(camera, EnsureCameraNumber(new MapRoomCameraMetadata(dockingIndex: dockingIndex)));
            if (camera is GlobalRootEntity globalRootCamera)
            {
                // Tracked like every camera of a loaded world, so that it is given a simulating player.
                worldEntityManager.AddOrUpdateGlobalRootEntity(globalRootCamera, false);
            }
        }
        RememberCameras(room);
    }

    /// <summary>
    ///     Called once a new room is stored. The cameras that came with it need a simulating player, who recharges
    ///     them: that is the builder, like for the room. The builder is told the numbers the cameras were given;
    ///     everyone else is sent the room after the numbers were set.
    /// </summary>
    public async Task CamerasBuiltAsync(Player builder, MapRoomEntity room)
    {
        using Hold held = await EnterAsync();
        foreach (WorldEntity camera in GetDockedCameras(room))
        {
            if (simulationOwnershipData.GetPlayerForLock(camera.Id) == null && simulationOwnershipData.TryAcquire(camera.Id, builder, SimulationLockType.TRANSIENT))
            {
                await SendToAllAsync(new SimulationOwnershipChange(camera.Id, builder.SessionId, SimulationLockType.TRANSIENT,
                    SimulationWhitelist.ShouldSimulateEntityMovement(camera)));
            }
            await ReplyAsync(builder, new EntityMetadataUpdate(camera.Id, ReadState(camera)));
        }
    }

    /// <summary>
    ///     Stores where rooms are in their base after the base changed shape.
    /// </summary>
    public void MoveRooms(IEnumerable<KeyValuePair<NitroxId, NitroxInt3>> cellsByRoomId)
    {
        using Hold held = Enter();
        foreach ((NitroxId roomId, NitroxInt3 cell) in cellsByRoomId)
        {
            if (TryGetRoom(roomId, out MapRoomEntity? room))
            {
                room.Cell = cell;
            }
        }
    }

    /// <summary>
    ///     Called after a room was deconstructed and taken out of the entity registry. Ends its scan and everything
    ///     that depended on the room: subscriptions, its simulation lock, and control and locks of its cameras. The
    ///     cameras in its docks are destroyed with it, as in the game; a camera out in the world outlives it.
    /// </summary>
    public async Task RoomDeconstructedAsync(Player sender, MapRoomEntity room)
    {
        using Hold held = await EnterAsync();
        await RemoveRoomInGateAsync(sender, room);
    }

    /// <summary>
    ///     Called after a whole base was deconstructed and taken out of the entity registry, for the rooms it had.
    /// </summary>
    public async Task BaseDeconstructedAsync(Player sender, NitroxId formerBaseId)
    {
        using Hold held = await EnterAsync();
        foreach (MapRoomEntity room in roomsById.Values.Where(room => room.ParentId == formerBaseId && !entityRegistry.TryGetEntityById(room.Id, out MapRoomEntity? _)).ToArray())
        {
            await RemoveRoomInGateAsync(sender, room);
        }
    }

    /// <summary>
    ///     Called before an entity is destroyed. A camera loses its controller; any entity leaves the scan results of
    ///     the rooms that had found it. A docked camera leaves its room when its entity is removed.
    /// </summary>
    public async Task EntityRemovedAsync(Player sender, NitroxId id)
    {
        using Hold held = await EnterAsync();
        CameraView camera = GetCamera(id);
        if (camera.Exists)
        {
            // Remembered beyond its removal: a request for it that is still on its way is then refused as one for a
            // camera that is gone, instead of being granted as a lock on an unknown id.
            knownCameraIds.TryAdd(id, 0);
            if (simulationOwnershipData.RevokeOwnerOfId(id, out SimulationOwnershipData.PlayerLock revokedLock) &&
                TryCreateControlRelease(id, revokedLock, "destroy", out MapRoomCameraControl? release))
            {
                await SendToAllAsync(release);
            }
            if (camera.DockedRoom != null)
            {
                diagnostics.RecordAccepted("destroy", camera.DockedRoom, id, sender.SessionId, camera.DockedSlot, "docked_camera_removed");
            }
        }

        await RemoveFromScanResultsAsync(id);
    }

    /// <summary>
    ///     Handles what picking an item up means for Scanner Rooms. Any item leaves the scan results of the rooms that
    ///     had found it. A camera also leaves its dock and its controller behind, in an order clients rely on, and its
    ///     light goes off, so its whole pickup is done here. Its state goes with it into the inventory.
    /// </summary>
    /// <returns>True if the item is a camera and nothing is left for the caller to do.</returns>
    public async Task<bool> EntityPickedUpAsync(Player sender, Entity item)
    {
        NitroxId id = item.Id;
        using Hold held = await EnterAsync();
        CameraView camera = GetCamera(id);
        if (!camera.Exists && !IsCamera(item))
        {
            await RemoveFromScanResultsAsync(id);
            return false;
        }

        knownCameraIds.TryAdd(id, 0);
        if (simulationOwnershipData.RevokeOwnerOfId(id, out SimulationOwnershipData.PlayerLock revokedLock))
        {
            if (TryCreateControlRelease(id, revokedLock, "pickup", out MapRoomCameraControl? release))
            {
                await SendToAllAsync(release);
            }
            await SendToAllAsync(new SimulationOwnershipChange(id, SessionId.SERVER_ID, SimulationLockType.TRANSIENT));
        }

        if (camera.WorldCamera is { } worldCamera)
        {
            if (camera.DockedRoom is { } room)
            {
                // The game takes a docked camera out of its dock before it goes into an inventory
                // (MapRoomCamera.OnPickedUp), and the player's report of that can still be on its way.
                diagnostics.RecordAccepted("pickup", room, id, sender.SessionId, camera.DockedSlot, "undocked");
                Undock(worldCamera, camera.State);
                await SendToAllAsync(new MapRoomCameraUndocked(id, room.Id));
            }
            // Do not track this entity in the open world anymore.
            worldEntityManager.StopTrackingEntity(worldCamera);
        }
        await RemoveFromScanResultsAsync(id);

        MapRoomCameraMetadata state = KeepCameraStateOnReplacementInGate(item);
        // The game switches a camera's light off when it is picked up (MapRoomCamera.OnPickedUp), in the game of the
        // player who picks it up. The stored state follows and reaches everyone else with the item, or they would be
        // shown a lit camera when it is dropped again.
        if (state.LightOn)
        {
            SetState(item, state.WithLight(false));
            diagnostics.RecordAccepted("light", cameraId: id, sessionId: sender.SessionId, reason: "off_pickup");
        }
        entityRegistry.AddOrUpdate(item);

        // Have other players respawn the item inside the inventory.
        await SendToOthersAsync(sender, new SpawnEntities(item, forceRespawn: true));
        return true;
    }

    /// <summary>
    ///     Whether the Scanner Rooms a base update names are the rooms of that base. A room that is not registered yet
    ///     must be the piece the update builds, taking the place of its construction ghost.
    /// </summary>
    internal static bool IsValidRoomTopology(EntityRegistry entityRegistry, UpdateBase update)
    {
        if (update.UpdatedMapRooms == null)
        {
            return false;
        }

        HashSet<NitroxInt3> cells = [];
        MapRoomEntity? builtRoom = update.BuiltPieceEntity as MapRoomEntity;
        bool builtRoomValidated = builtRoom == null;
        foreach ((NitroxId roomId, NitroxInt3 cell) in update.UpdatedMapRooms)
        {
            if (!cells.Add(cell))
            {
                return false;
            }
            if (!entityRegistry.TryGetEntityById(roomId, out MapRoomEntity room))
            {
                if (builtRoom == null ||
                    builtRoomValidated ||
                    builtRoom.Id != roomId ||
                    builtRoom.Id != update.FormerGhostId ||
                    builtRoom.Cell != cell ||
                    builtRoom.ParentId != update.BaseId ||
                    !IsPendingRoomGhost(entityRegistry, update))
                {
                    return false;
                }
                room = builtRoom;
                builtRoomValidated = true;
            }
            if (!IsAllowedRoomParent(room.ParentId, update.BaseId, update.ChildrenTransfer))
            {
                return false;
            }
        }
        return builtRoomValidated;
    }

    internal static bool IsAllowedRoomParent(NitroxId? roomParentId, NitroxId baseId, (NitroxId From, NitroxId To) transfer) =>
        roomParentId == baseId || (transfer.To == baseId && transfer.From != null && roomParentId == transfer.From);

    private static bool IsPendingRoomGhost(EntityRegistry entityRegistry, UpdateBase update) =>
        entityRegistry.TryGetEntityById(update.FormerGhostId, out GhostEntity ghost) &&
        ghost.TechType?.Name == "BaseMapRoom" &&
        ghost.ParentId == update.BaseId;

    private async Task RemoveRoomInGateAsync(Player sender, MapRoomEntity mapRoom)
    {
        RememberCameras(mapRoom);
        // Nobody is told that the results are gone: every client drops them together with its own copy of the room.
        scansByRoomId.Remove(mapRoom.Id);
        subscribers.RemoveWhere(subscriber => subscriber.RoomId == mapRoom.Id);

        // The room may have been removed together with its base, which only stops tracking the base itself. A room
        // that stayed tracked would be handed to every joining player as something to simulate.
        worldEntityManager.RemoveGlobalRootEntity(mapRoom.Id, false);
        simulationOwnershipData.RevokeOwnerOfId(mapRoom.Id);
        await SendToAllAsync(new DropSimulationOwnership([mapRoom.Id]));

        // The room took its children out of the entity registry with it, the cameras in its docks among them. That is
        // what the game does (MapRoomCameraDocking.OnDestroy), and a room that is built again comes with two new
        // cameras. What is left of them here is who simulated and who flew them. A camera that was out of its dock
        // is an entity of the world and stays.
        WorldEntity[] cameras = GetDockedCameras(mapRoom);
        foreach (WorldEntity camera in cameras)
        {
            if (camera is GlobalRootEntity)
            {
                worldEntityManager.RemoveGlobalRootEntity(camera.Id, false);
            }
            if (simulationOwnershipData.RevokeOwnerOfId(camera.Id, out SimulationOwnershipData.PlayerLock revokedLock) &&
                TryCreateControlRelease(camera.Id, revokedLock, "deconstruct", new CameraView(camera, ReadState(camera), mapRoom), out MapRoomCameraControl? release))
            {
                await SendToAllAsync(release);
            }
            await SendToAllAsync(new DropSimulationOwnership([camera.Id]));
            await RemoveFromScanResultsAsync(camera.Id);
            await SendToAllAsync(new EntityDestroyed(camera.Id));
        }
        if (roomsById.TryGetValue(mapRoom.Id, out MapRoomEntity? tracked) && ReferenceEquals(tracked, mapRoom))
        {
            roomsById.Remove(mapRoom.Id);
        }
        diagnostics.RecordAccepted("deconstruct", mapRoom, sessionId: sender.SessionId, reason: "locks_reconciled");
        logger.ZLogInformation($"Cleared results, subscriptions, room lock and {cameras.Length} docked camera(s) while deconstructing Scanner Room {mapRoom.Id}");
    }
}
