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
        // The room keeps the id it had if it was deconstructed and is built again.
        RaiseAboveFloor(room);
        roomsById[room.Id] = room;
        RememberCameras(room);
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
    ///     Called before an entity is destroyed. A camera loses its controller, its dock and its room record, in that
    ///     order for clients; any entity leaves the scan results of the rooms that had found it.
    /// </summary>
    public async Task EntityRemovedAsync(Player sender, NitroxId id)
    {
        using Hold held = await EnterAsync();
        if (IsScannerCamera(id) && GetTopology(id).IsScannerCamera)
        {
            // Remembered beyond its removal: a request for it that is still on its way is then refused as one for a
            // camera that is gone, instead of being granted as a lock on an unknown id.
            knownCameraIds.TryAdd(id, 0);
            if (simulationOwnershipData.RevokeOwnerOfId(id, out SimulationOwnershipData.PlayerLock revokedLock) &&
                TryCreateControlRelease(id, revokedLock, "destroy", out MapRoomCameraControl? release))
            {
                await SendToAllAsync(release);
            }
        }

        foreach (MapRoomEntity room in GetRooms())
        {
            MapRoomCameraRecord? record = room.GetCameraRecord(id);
            if (record != null)
            {
                // The id of a room's default camera is derived from the room and comes back if the room is rebuilt.
                RememberFloor(record);
            }
            if (room.RemoveCamera(id, out int dockingIndex) && dockingIndex >= 0)
            {
                diagnostics.RecordAccepted("destroy", room, id, sender.SessionId, dockingIndex, "registry_removed");
                await SendToAllAsync(new MapRoomCameraDock(id, room.Id, dockingIndex, room.DockingRevision, true, true, false,
                    record?.CameraNumber ?? 0, false, record?.LightRevision ?? 0, record?.Energy ?? 0f, 0f, record?.ComponentRevision ?? 0));
            }
        }

        await RemoveFromScanResultsAsync(id);
    }

    /// <summary>
    ///     Handles what picking an item up means for Scanner Rooms. Any item leaves the scan results of the rooms that
    ///     had found it. A camera also leaves its dock, its controller and its light behind, in an order clients rely
    ///     on, so its whole pickup is done here.
    /// </summary>
    /// <returns>True if the item is a camera and nothing is left for the caller to do.</returns>
    public async Task<bool> EntityPickedUpAsync(Player sender, Entity item)
    {
        NitroxId id = item.Id;
        using Hold held = await EnterAsync();
        if (!IsScannerCamera(id) && !mapRoomCameraTechType.Equals(item.TechType))
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

        if (entityRegistry.TryGetEntityById(id, out WorldEntity? worldEntity))
        {
            // Do not track this entity in the open world anymore.
            worldEntityManager.StopTrackingEntity(worldEntity);
        }
        await RemoveFromScanResultsAsync(id);

        KeepCameraStateOnReplacementInGate(item);
        // The game switches a camera's light off when it is picked up (MapRoomCamera.OnPickedUp). The stored state
        // follows, or everyone but the player holding the camera would be shown a lit camera when it is dropped again.
        MapRoomCameraLight? lightOff = null;
        if (item.Metadata is MapRoomCameraMetadata { LightOn: true })
        {
            lightOff = new MapRoomCameraLight(id, false, SetLooseLight(item, false, out _).LightRevision, true, true);
        }
        entityRegistry.AddOrUpdate(item);

        foreach (MapRoomEntity room in GetRooms())
        {
            if (room.GetCameraRecord(id) is { LightOn: true } litRecord)
            {
                litRecord.LightOn = false;
                litRecord.LightRevision++;
                lightOff = new MapRoomCameraLight(id, false, litRecord.LightRevision, true, true);
            }
            if (room.TryClearDockedCamera(id, out int dockingIndex))
            {
                MapRoomCameraRecord? record = room.GetCameraRecord(id);
                diagnostics.RecordAccepted("pickup", room, id, sender.SessionId, dockingIndex, "undocked");
                await SendToAllAsync(new MapRoomCameraDock(id, room.Id, dockingIndex, room.DockingRevision, true, true, false,
                    record?.CameraNumber ?? 0, record?.LightOn ?? false, record?.LightRevision ?? 0,
                    record?.Energy ?? MapRoomCameraRecord.MAX_ENERGY,
                    record?.Health ?? MapRoomCameraRecord.MAX_HEALTH, record?.ComponentRevision ?? 0));
            }
        }

        if (lightOff != null)
        {
            await SendToAllAsync(lightOff);
        }

        // Have other players respawn the item inside the inventory.
        await SendToOthersAsync(sender, new SpawnEntities(item, forceRespawn: true));
        return true;
    }

    /// <summary>
    ///     How each of the given ids is on record as a camera, read at one moment, for checking a batch of movement
    ///     reports against.
    /// </summary>
    public IReadOnlyDictionary<NitroxId, CameraRegistrationState> GetCameraRegistrationStates(IEnumerable<NitroxId?> ids)
    {
        Dictionary<NitroxId, CameraRegistrationState> states = [];
        foreach (NitroxId? id in ids)
        {
            if (id != null)
            {
                states[id] = default;
            }
        }

        using Hold held = Enter();
        foreach (MapRoomEntity room in GetRooms())
        {
            foreach (MapRoomCameraRecord? record in room.CameraRegistry)
            {
                if (record?.CameraId != null && states.TryGetValue(record.CameraId, out CameraRegistrationState state))
                {
                    states[record.CameraId] = state with { Registrations = state.Registrations + 1 };
                }
            }
            foreach (NitroxId? dockedCameraId in new[] { room.LeftDockCameraId, room.RightDockCameraId })
            {
                if (dockedCameraId != null && states.TryGetValue(dockedCameraId, out CameraRegistrationState state))
                {
                    states[dockedCameraId] = state with { DockedAssociations = state.DockedAssociations + 1 };
                }
            }
        }
        return states;
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

    /// <summary>
    ///     Puts every camera the room has a record for, but that exists nowhere (in no dock and as no entity), back
    ///     into a free dock of the room, preferring the dock its camera number names.
    /// </summary>
    /// <returns>How many cameras were put back.</returns>
    internal static int RestoreOrphanedRegistrations(MapRoomEntity mapRoom, Func<NitroxId, bool> entityExists)
    {
        int restored = 0;
        foreach (MapRoomCameraRecord record in mapRoom.CameraRegistry.OrderBy(record => record.CameraNumber).ToList())
        {
            if (entityExists(record.CameraId) || mapRoom.IsCameraDocked(record.CameraId))
            {
                continue;
            }

            int preferredSlot = record.CameraNumber - 1;
            int slot = preferredSlot is >= 0 and < MapRoomEntity.DOCK_COUNT && mapRoom.GetDockedCamera(preferredSlot) == null
                ? preferredSlot
                : mapRoom.LeftDockCameraId == null ? 0
                : mapRoom.RightDockCameraId == null ? 1
                : -1;
            if (slot < 0)
            {
                break;
            }

            mapRoom.SetDockedCamera(slot, record.CameraId);
            restored++;
        }
        return restored;
    }

    private static bool IsPendingRoomGhost(EntityRegistry entityRegistry, UpdateBase update) =>
        entityRegistry.TryGetEntityById(update.FormerGhostId, out GhostEntity ghost) &&
        ghost.TechType?.Name == "BaseMapRoom" &&
        ghost.ParentId == update.BaseId;

    private async Task RemoveRoomInGateAsync(Player sender, MapRoomEntity mapRoom)
    {
        RememberCameras(mapRoom);
        // The room keeps its id if it is built again.
        RememberFloor(mapRoom);
        // Nobody is told that the results are gone: every client drops them together with its own copy of the room.
        scansByRoomId.Remove(mapRoom.Id);
        subscribers.RemoveWhere(subscriber => subscriber.RoomId == mapRoom.Id);

        // The room may have been removed together with its base, which only stops tracking the base itself. A room
        // that stayed tracked would be handed to every joining player as something to simulate.
        worldEntityManager.RemoveGlobalRootEntity(mapRoom.Id, false);
        simulationOwnershipData.RevokeOwnerOfId(mapRoom.Id);
        await SendToAllAsync(new DropSimulationOwnership([mapRoom.Id]));
        MapRoomCameraRecord[] cameras = mapRoom.CameraRegistry.ToArray();
        HashSet<NitroxId> dockedCameraIds = cameras.Select(camera => camera.CameraId).Where(mapRoom.IsCameraDocked).ToHashSet();
        foreach (MapRoomCameraRecord camera in cameras)
        {
            if (IsRegisteredWithAnotherLiveRoom(mapRoom, camera.CameraId))
            {
                continue;
            }
            bool isDocked = dockedCameraIds.Contains(camera.CameraId);
            bool hadLock = simulationOwnershipData.RevokeOwnerOfId(camera.CameraId, out SimulationOwnershipData.PlayerLock revokedLock);
            if (hadLock && TryCreateControlRelease(camera.CameraId, revokedLock, "deconstruct", out MapRoomCameraControl? release))
            {
                await SendToAllAsync(release);
            }
            // A camera out in the world outlives the room and still needs a simulating player. Control has ended above;
            // whoever held the lock keeps simulating the camera, because a plain transient lock cannot be requested
            // for a Scanner Room camera afterwards.
            if (hadLock && !isDocked && GetTopology(camera.CameraId).IsLooseWorldCamera && TryGetWorldCamera(camera.CameraId, out WorldEntity? looseCamera) &&
                simulationOwnershipData.TryAcquire(camera.CameraId, revokedLock.Player, SimulationLockType.TRANSIENT))
            {
                await SendToAllAsync(new SimulationOwnershipChange(camera.CameraId, revokedLock.Player.SessionId,
                    SimulationLockType.TRANSIENT, SimulationWhitelist.ShouldSimulateEntityMovement(looseCamera)));
            }
            else
            {
                await SendToAllAsync(new DropSimulationOwnership([camera.CameraId]));
            }
            if (!isDocked)
            {
                // Its light, battery and health were only in the room's record, which is about to be gone, so they
                // move onto the camera itself.
                MoveStateOutOfRecord(camera);
                continue;
            }
            // A default camera of the room built again gets the same id.
            RememberFloor(camera);
            // The game destroys a docked camera together with its dock. Without removing the server's copy, a camera
            // that had ever been undocked would come back for later joiners as a loose camera at a stale position.
            if (worldEntityManager.TryDestroyEntity(camera.CameraId, out _))
            {
                await RemoveFromScanResultsAsync(camera.CameraId);
                await SendToAllAsync(new EntityDestroyed(camera.CameraId));
            }
        }
        if (roomsById.TryGetValue(mapRoom.Id, out MapRoomEntity? tracked) && ReferenceEquals(tracked, mapRoom))
        {
            roomsById.Remove(mapRoom.Id);
        }
        diagnostics.RecordAccepted("deconstruct", mapRoom, sessionId: sender.SessionId, reason: "locks_reconciled");
        logger.ZLogInformation($"Cleared results, subscriptions, room lock, and {cameras.Length} camera registration(s) while deconstructing Scanner Room {mapRoom.Id}");
    }

    private bool IsRegisteredWithAnotherLiveRoom(MapRoomEntity deconstructingRoom, NitroxId cameraId) =>
        GetRooms().Exists(room => !ReferenceEquals(room, deconstructingRoom) && room.Id != deconstructingRoom.Id && room.GetCameraRecord(cameraId) != null);

    /// <summary>
    ///     In how many rooms an id has a camera record and in how many docks it sits.
    /// </summary>
    internal readonly record struct CameraRegistrationState(int Registrations, int DockedAssociations)
    {
        internal bool HasAssociation => Registrations > 0 || DockedAssociations > 0;

        internal bool IsAmbiguous => Registrations > 1 || DockedAssociations > 1 || DockedAssociations == 1 && Registrations != 1;

        internal bool IsDocked => !IsAmbiguous && DockedAssociations == 1;

        internal bool IsExactlyOneRegistration => !IsAmbiguous && Registrations == 1;
    }
}
