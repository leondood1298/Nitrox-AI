using System.Linq;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Which camera sits in which dock.
/// </summary>
internal sealed partial class ScannerRoomManager
{
    /// <summary>
    ///     Handles a player's report that a camera went into a dock, and tells everyone if it did.
    /// </summary>
    /// <returns>Whether the camera sits in that dock now.</returns>
    public Task<bool> DockCameraAsync(Player sender, MapRoomCameraDock request) => ChangeDockingAsync(sender, request);

    /// <summary>
    ///     Handles a player's report that a camera left its dock, and tells everyone if it did.
    /// </summary>
    /// <returns>Whether that dock is free of the camera now.</returns>
    public Task<bool> UndockCameraAsync(Player sender, MapRoomCameraDock request) => ChangeDockingAsync(sender, request);

    internal static bool IsKnownCamera(bool validWorldCamera, bool registeredCamera) => validWorldCamera || registeredCamera;

    /// <summary>
    ///     Only a camera out in the world, or a default camera that exists as nothing but its room's record, can sit in
    ///     a dock. A camera in an inventory keeps its room record, and a dock report can cross the pickup on the way to
    ///     the server; the id of any other kind of entity is never a camera, whoever reports it.
    /// </summary>
    internal static bool CanBeDocked(bool isDocked, bool entityExists, bool validWorldCamera) =>
        !isDocked || !entityExists || validWorldCamera;

    internal static bool CanBootstrapRestoredCamera(bool isDocked, bool senderOwnsRoom, bool slotAvailable, int registeredCameraCount) =>
        isDocked && senderOwnsRoom && slotAvailable && registeredCameraCount is >= 0 and < MapRoomEntity.DOCK_COUNT;

    internal static bool ShouldPreserveControlLock(SimulationLockType lockType, bool holderIsActiveController) =>
        lockType == SimulationLockType.EXCLUSIVE && holderIsActiveController;

    internal static bool ShouldPersistLooseCamera(bool isDocked, bool entityExists, bool hasTransform) =>
        !isDocked && !entityExists && hasTransform;

    internal static bool IsRequestedStateCanonical(bool isDocked, NitroxId cameraId, NitroxId? slotCameraId, bool cameraDocked) =>
        isDocked ? slotCameraId == cameraId : slotCameraId == null && !cameraDocked;

    private async Task<bool> ChangeDockingAsync(Player sender, MapRoomCameraDock packet)
    {
        string eventName = packet.IsDocked ? "dock" : "undock";
        using Hold held = await EnterAsync();
        bool roomWasKnown = packet.MapRoomId != null && roomsById.ContainsKey(packet.MapRoomId);
        TryGetRoom(packet.MapRoomId, out MapRoomEntity? mapRoom);
        string? refusal = packet.IsServerResponse ? "server_response" :
                          packet.DockingIndex is < 0 or >= MapRoomEntity.DOCK_COUNT ? "invalid_slot" :
                          mapRoom != null ? null :
                          roomWasKnown ? "room_removed" : "unknown_room";
        if (refusal != null || mapRoom == null)
        {
            diagnostics.RecordRejected(eventName, mapRoom, packet.CameraId, sender.SessionId, packet.DockingIndex, refusal);
            await ReplyAsync(sender, CreateDockRefusal(packet, 0));
            return false;
        }

        bool entityExists = entityRegistry.TryGetEntityById(packet.CameraId, out Entity? existingEntity);
        bool validWorldCamera = existingEntity is WorldEntity && mapRoomCameraTechType.Equals(existingEntity.TechType);
        bool senderOwnsRoom = simulationOwnershipData.GetPlayerForLock(mapRoom.Id) == sender;
        bool registeredCamera = mapRoom.GetCameraRecord(packet.CameraId) != null;
        bool canBootstrapRestoredCamera = CanBootstrapRestoredCamera(packet.IsDocked, senderOwnsRoom,
            mapRoom.GetDockedCamera(packet.DockingIndex) == null, mapRoom.CameraRegistry.Count);
        // Checked before the id is remembered as a Scanner Room camera: that routes every later simulation request
        // for the id to the camera rules, for good. For the id of a vehicle it would mean nobody can pilot it again.
        if ((!IsKnownCamera(validWorldCamera, registeredCamera) && !canBootstrapRestoredCamera) ||
            !CanBeDocked(packet.IsDocked, entityExists, validWorldCamera))
        {
            diagnostics.RecordRejected(eventName, mapRoom, packet.CameraId, sender.SessionId, packet.DockingIndex, "unknown_camera");
            await ReplyAsync(sender, CreateDockRefusal(packet, 0));
            return false;
        }
        knownCameraIds.TryAdd(packet.CameraId, 0);

        NitroxId? occupyingCamera = mapRoom.GetDockedCamera(packet.DockingIndex);
        bool alreadyCanonical = IsRequestedStateCanonical(packet.IsDocked, packet.CameraId, occupyingCamera, mapRoom.IsCameraDocked(packet.CameraId));
        bool senderOwnsCamera = simulationOwnershipData.GetPlayerForLock(packet.CameraId) == sender;
        bool senderAuthorized = senderOwnsCamera || senderOwnsRoom;
        bool granted = alreadyCanonical;
        bool stateChanged = false;
        string diagnosticReason = "slot_conflict";
        if (!alreadyCanonical && !senderAuthorized)
        {
            diagnosticReason = "non_owner";
        }
        else if (!alreadyCanonical && !packet.IsDocked)
        {
            granted = mapRoom.TryClearDockedCamera(packet.DockingIndex, packet.CameraId);
            stateChanged = granted;
        }
        else if (!alreadyCanonical)
        {
            bool localSlotAvailable = (occupyingCamera == null || occupyingCamera == packet.CameraId) && (!mapRoom.IsCameraDocked(packet.CameraId) || occupyingCamera == packet.CameraId);
            granted = localSlotAvailable && TryTransferRegistration(mapRoom, packet.CameraId);
            if (granted && occupyingCamera == null)
            {
                mapRoom.SetDockedCamera(packet.DockingIndex, packet.CameraId);
                stateChanged = true;
            }
        }
        MapRoomCameraRecord? existingRecord = granted ? mapRoom.GetCameraRecord(packet.CameraId) : null;
        int cameraNumber = granted
            ? existingRecord?.CameraNumber ?? (stateChanged ? mapRoom.GetOrAssignCameraNumber(packet.CameraId, packet.DockingIndex + 1) : 0)
            : 0;
        MapRoomCameraRecord? record = granted ? mapRoom.GetCameraRecord(packet.CameraId) : null;
        if (existingRecord == null && record != null)
        {
            // First registration with a room: the record takes over the state the camera had on its own.
            MoveStateIntoRecord(packet.CameraId, record);
            RaiseAboveFloor(record);
        }
        MapRoomCameraDock response = new(packet.CameraId, packet.MapRoomId, packet.DockingIndex, mapRoom.DockingRevision, true, granted, packet.IsDocked, cameraNumber,
            record?.LightOn ?? false, record?.LightRevision ?? 0, record?.Energy ?? MapRoomCameraRecord.MAX_ENERGY, record?.Health ?? MapRoomCameraRecord.MAX_HEALTH,
            record?.ComponentRevision ?? 0);
        diagnosticReason = granted ? alreadyCanonical ? "idempotent" : existingRecord == null && !validWorldCamera ? "bootstrap" : "changed" : diagnosticReason;
        if (!granted)
        {
            diagnostics.RecordRejected(eventName, mapRoom, packet.CameraId, sender.SessionId, packet.DockingIndex, diagnosticReason);
            await ReplyAsync(sender, response);
            return false;
        }
        diagnostics.RecordAccepted(eventName, mapRoom, packet.CameraId, sender.SessionId, packet.DockingIndex, diagnosticReason);

        if (senderAuthorized && ShouldPersistLooseCamera(packet.IsDocked, entityExists, packet.CameraTransform != null))
        {
            GlobalRootEntity looseCamera = new(packet.CameraTransform!, GlobalRootEntity.GLOBAL_ROOT_LEVEL, MAP_ROOM_CAMERA_CLASS_ID, true,
                packet.CameraId, mapRoomCameraTechType, null, null, []);
            entityRegistry.AddOrUpdate(looseCamera);
            worldEntityManager.TrackEntityInTheWorld(looseCamera);
            logger.ZLogInformation($"Persisted restored camera {packet.CameraId} as a loose world entity after its first undock");
        }
        MapRoomCameraControl? controlRelease = null;
        bool preserveControlLock = false;
        if (stateChanged && packet.IsDocked && simulationOwnershipData.TryGetLock(packet.CameraId, out SimulationOwnershipData.PlayerLock playerLock))
        {
            // Whoever reports the dock: every client whose own physics sees the camera enter the dock does, and
            // the room owner's report can arrive before the one of the player flying the camera in. That player
            // keeps control while docked, as in the game.
            preserveControlLock = ShouldPreserveControlLock(playerLock.LockType, IsController(packet.CameraId, playerLock.Player.SessionId));
        }
        bool lockRevoked = false;
        if (stateChanged && packet.IsDocked && !preserveControlLock &&
            simulationOwnershipData.RevokeOwnerOfId(packet.CameraId, out SimulationOwnershipData.PlayerLock revokedLock))
        {
            lockRevoked = true;
            TryCreateControlRelease(packet.CameraId, revokedLock, "dock", out controlRelease);
        }
        if (controlRelease != null)
        {
            await SendToAllAsync(controlRelease);
        }
        if (lockRevoked)
        {
            // A docked camera is simulated by nobody. The player who simulated it until now (after letting go of
            // its controls, for example) has to be told, or their client keeps counting the camera as its own.
            await SendToAllAsync(new DropSimulationOwnership([packet.CameraId]));
        }
        await SendToAllAsync(response);
        return true;
    }

    private static MapRoomCameraDock CreateDockRefusal(MapRoomCameraDock packet, long dockingRevision) =>
        new(packet.CameraId, packet.MapRoomId, packet.DockingIndex, dockingRevision, true, false, packet.IsDocked);

    /// <summary>
    ///     A camera belongs to the room it was docked in last. Docking it in another room moves its record, with its
    ///     state, to that room, unless it still sits in a dock elsewhere.
    /// </summary>
    private bool TryTransferRegistration(MapRoomEntity targetRoom, NitroxId cameraId)
    {
        MapRoomEntity? sourceRoom = null;
        MapRoomCameraRecord? sourceRecord = null;
        foreach (MapRoomEntity room in GetRooms())
        {
            if (room == targetRoom)
            {
                continue;
            }
            MapRoomCameraRecord? record = room.GetCameraRecord(cameraId);
            if (room.IsCameraDocked(cameraId) || (record != null && sourceRecord != null))
            {
                return false;
            }
            if (record != null)
            {
                sourceRoom = room;
                sourceRecord = record;
            }
        }
        if (sourceRoom == null || sourceRecord == null)
        {
            return true;
        }
        if (!sourceRoom.CameraRegistry.Remove(sourceRecord))
        {
            return false;
        }
        if (targetRoom.CameraRegistry.Exists(record => record.CameraNumber == sourceRecord.CameraNumber))
        {
            sourceRecord.CameraNumber = targetRoom.CameraRegistry.Count == 0 ? 1 : targetRoom.CameraRegistry.Max(record => record.CameraNumber) + 1;
        }
        targetRoom.CameraRegistry.Add(sourceRecord);
        logger.ZLogInformation($"Transferred camera {cameraId} registration from Scanner Room {sourceRoom.Id} to {targetRoom.Id} with number {sourceRecord.CameraNumber}");
        return true;
    }
}
