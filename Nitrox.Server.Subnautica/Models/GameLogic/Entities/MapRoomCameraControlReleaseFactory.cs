using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <summary>
///     Creates the canonical control-release packet that must accompany revocation of an
///     exclusive Scanner Room camera simulation lock. Generic simulation ownership packets
///     cannot safely stand in for this transition because they do not identify control state.
/// </summary>
internal sealed class MapRoomCameraControlReleaseFactory(EntityRegistry entityRegistry, ScannerRoomDiagnostics diagnostics,
    MapRoomCameraControlLifecycle controlLifecycle)
{
    private static readonly NitroxTechType mapRoomCameraTechType = new("MapRoomCamera");

    public bool TryCreate(NitroxId cameraId, SimulationOwnershipData.PlayerLock revokedLock, string reason,
        out MapRoomCameraControl release)
    {
        release = null!;
        // Clear any acquisition state even if an earlier bug or future lifecycle path changed the lock type
        // before cleanup observed it. Only exclusive revocations produce a control-release packet.
        controlLifecycle.EndPreviewAcquisition(cameraId, revokedLock.Player.SessionId);
        if (revokedLock.LockType != SimulationLockType.EXCLUSIVE)
        {
            return false;
        }

        MapRoomEntity? registeredRoom = null;
        MapRoomEntity? dockedRoom = null;
        int dockedSlot = -1;
        bool lightOn = false;
        int registrations = 0;

        foreach (MapRoomEntity room in entityRegistry.GetEntities<MapRoomEntity>())
        {
            lock (room)
            {
                MapRoomCameraRecord? record = room.GetCameraRecord(cameraId);
                if (record == null)
                {
                    continue;
                }

                registrations++;
                registeredRoom ??= room;
                lock (record)
                {
                    lightOn = record.LightOn;
                }

                int slot = room.LeftDockCameraId == cameraId ? 0 : room.RightDockCameraId == cameraId ? 1 : -1;
                if (slot >= 0)
                {
                    dockedRoom = room;
                    dockedSlot = slot;
                }
            }
        }

        bool isWorldCamera = entityRegistry.TryGetEntityById(cameraId, out WorldEntity worldEntity) &&
                             mapRoomCameraTechType.Equals(worldEntity.TechType);
        if (registrations == 0 && !isWorldCamera)
        {
            return false;
        }

        if (registrations > 1)
        {
            diagnostics.RecordInvariantFailure("control_revoke", registeredRoom, cameraId, revokedLock.Player.SessionId,
                reason: "duplicate_registration");
        }

        Optional<NitroxId> roomId = dockedRoom == null ? Optional.Empty : Optional.Of(dockedRoom.Id);
        release = new MapRoomCameraControl(cameraId, roomId, dockedSlot, false, lightOn, true, true, revokedLock.Player.SessionId);
        diagnostics.RecordAccepted("control_revoke", dockedRoom ?? registeredRoom, cameraId, revokedLock.Player.SessionId,
            dockedSlot, reason);
        return true;
    }

    public bool IsScannerCamera(NitroxId cameraId)
    {
        if (entityRegistry.TryGetEntityById(cameraId, out WorldEntity worldEntity) &&
            mapRoomCameraTechType.Equals(worldEntity.TechType))
        {
            return true;
        }

        foreach (MapRoomEntity room in entityRegistry.GetEntities<MapRoomEntity>())
        {
            lock (room)
            {
                if (room.GetCameraRecord(cameraId) != null)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    ///     Generic ownership is legitimate only for Stalker interaction with a coherent loose world
    ///     camera, or as an idempotent exclusive refresh by the controller already validated through
    ///     the canonical MapRoomCameraControl flow. A generic transient request may release a
    ///     Stalker-purpose exclusive lock, but it cannot stand in for the canonical camera-control
    ///     release packet.
    /// </summary>
    public bool CanGrantGenericOwnership(NitroxId cameraId, SessionId requesterSessionId,
        SimulationLockType requestedLock, bool requesterOwnsExclusive)
    {
        CameraTopology topology = ResolveCameraTopology(cameraId);
        if (topology.IsAmbiguous || !topology.IsKnownScannerCamera)
        {
            return false;
        }

        bool requesterIsActiveController =
            controlLifecycle.IsActiveController(cameraId, requesterSessionId);
        bool hasActiveController = controlLifecycle.HasActiveController(cameraId);
        return requestedLock switch
        {
            SimulationLockType.EXCLUSIVE when hasActiveController =>
                requesterIsActiveController && requesterOwnsExclusive,
            SimulationLockType.EXCLUSIVE => topology.IsLooseWorldCamera,
            SimulationLockType.TRANSIENT =>
                !hasActiveController && requesterOwnsExclusive && topology.IsLooseWorldCamera,
            _ => false
        };
    }

    private CameraTopology ResolveCameraTopology(NitroxId cameraId)
    {
        bool validWorldCamera =
            entityRegistry.TryGetEntityById(cameraId, out WorldEntity worldEntity) &&
            mapRoomCameraTechType.Equals(worldEntity.TechType);
        int registrations = 0;
        int dockedAssociations = 0;
        foreach (MapRoomEntity room in entityRegistry.GetEntities<MapRoomEntity>())
        {
            lock (room)
            {
                if (room.GetCameraRecord(cameraId) != null)
                {
                    registrations++;
                }
                if (room.IsCameraDocked(cameraId))
                {
                    dockedAssociations++;
                }
            }
        }

        bool isAmbiguous = registrations > 1 || dockedAssociations > 1 ||
                           dockedAssociations == 1 && registrations != 1;
        bool isKnownScannerCamera = !isAmbiguous && (validWorldCamera || registrations == 1);
        bool isLooseWorldCamera =
            isKnownScannerCamera && validWorldCamera && dockedAssociations == 0;
        return new CameraTopology(isKnownScannerCamera, isLooseWorldCamera, isAmbiguous);
    }

    private readonly record struct CameraTopology(
        bool IsKnownScannerCamera,
        bool IsLooseWorldCamera,
        bool IsAmbiguous);

    public IReadOnlyList<NitroxId> GetScannerCameraIds()
    {
        HashSet<NitroxId> ids = entityRegistry.GetEntities<WorldEntity>()
                                              .Where(entity => mapRoomCameraTechType.Equals(entity.TechType))
                                              .Select(entity => entity.Id)
                                              .ToHashSet();
        foreach (MapRoomEntity room in entityRegistry.GetEntities<MapRoomEntity>())
        {
            lock (room)
            {
                foreach (MapRoomCameraRecord record in room.CameraRegistry)
                {
                    ids.Add(record.CameraId);
                }
            }
        }
        return ids.ToArray();
    }
}
