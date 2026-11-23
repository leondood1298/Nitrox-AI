using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Helper;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

internal sealed class MapRoomDeconstructionCleanup(EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData,
    MapRoomCameraControlReleaseFactory cameraControlReleaseFactory, MapRoomScanResultSubscriptions subscriptions,
    MapRoomCameraControlLifecycle controlLifecycle, PlayerManager playerManager, ScannerRoomDiagnostics diagnostics,
    WorldEntityManager worldEntityManager, MapRoomLooseCameraState looseCameraState, MapRoomRevisionFloors revisionFloors)
{
    public async Task CleanupAsync(MapRoomEntity mapRoom, AuthProcessorContext context)
    {
        // The room keeps its id if it is built again.
        revisionFloors.Remember(mapRoom);
        MapRoomScanResultSnapshot clearedResults;
        lock (mapRoom)
        {
            mapRoom.BeginScanResultGeneration(mapRoom.ScanResultGeneration + 1);
            clearedResults = new MapRoomScanResultSnapshot(mapRoom.Id, mapRoom.ScanResultGeneration, [], NitroxVector3.Zero, 0f,
                mapRoom.ScanResultRevision, true, true);
        }
        var subscribedSessions = subscriptions.RemoveRoom(mapRoom.Id);
        foreach (Player player in playerManager.GetConnectedPlayers().Where(player => subscribedSessions.Contains(player.SessionId)))
        {
            await context.SendAsync(clearedResults, player.SessionId);
        }

        // The room may have been removed together with its base, which only stops tracking the base itself. A room
        // that stayed tracked would be handed to every joining player as something to simulate.
        worldEntityManager.RemoveGlobalRootEntity(mapRoom.Id, false);
        simulationOwnershipData.RevokeOwnerOfId(mapRoom.Id);
        await context.SendToAllAsync(new DropSimulationOwnership([mapRoom.Id]));
        MapRoomCameraRecord[] cameras;
        HashSet<NitroxId> dockedCameraIds;
        lock (mapRoom)
        {
            cameras = mapRoom.CameraRegistry.ToArray();
            dockedCameraIds = cameras.Select(camera => camera.CameraId).Where(mapRoom.IsCameraDocked).ToHashSet();
        }
        foreach (MapRoomCameraRecord camera in cameras)
        {
            using IDisposable lifecycleGate = await controlLifecycle.EnterAsync(camera.CameraId);
            if (IsRegisteredWithAnotherLiveRoom(mapRoom, camera.CameraId))
            {
                continue;
            }
            bool hadLock = simulationOwnershipData.RevokeOwnerOfId(camera.CameraId, out SimulationOwnershipData.PlayerLock revokedLock);
            if (hadLock && cameraControlReleaseFactory.TryCreate(camera.CameraId, revokedLock, "deconstruct", out MapRoomCameraControl release))
            {
                await context.SendToAllAsync(release);
            }
            // A camera out in the world outlives the room and still needs a simulating player. Control has ended above;
            // whoever held the lock keeps simulating the camera, because a plain transient lock cannot be requested
            // for a Scanner Room camera afterwards.
            if (hadLock && !dockedCameraIds.Contains(camera.CameraId) &&
                cameraControlReleaseFactory.TryGetLooseWorldCamera(camera.CameraId, out WorldEntity? looseCamera) &&
                simulationOwnershipData.TryAcquire(camera.CameraId, revokedLock.Player, SimulationLockType.TRANSIENT))
            {
                await context.SendToAllAsync(new SimulationOwnershipChange(camera.CameraId, revokedLock.Player.SessionId,
                    SimulationLockType.TRANSIENT, SimulationWhitelist.ShouldSimulateEntityMovement(looseCamera)));
            }
            else
            {
                await context.SendToAllAsync(new DropSimulationOwnership([camera.CameraId]));
            }
            // The game destroys a docked camera together with its dock. Without removing the server's copy, a camera
            // that had ever been undocked would come back for later joiners as a loose camera at a stale position.
            if (dockedCameraIds.Contains(camera.CameraId))
            {
                // A default camera of the room built again gets the same id.
                revisionFloors.Remember(camera);
            }
            if (dockedCameraIds.Contains(camera.CameraId) && worldEntityManager.TryDestroyEntity(camera.CameraId, out _))
            {
                await context.SendToAllAsync(new EntityDestroyed(camera.CameraId));
            }
            else if (!dockedCameraIds.Contains(camera.CameraId))
            {
                // A camera out in the world outlives the room. Its light, battery and health were only in the
                // room's record, which is about to be gone, so they move onto the camera itself.
                looseCameraState.MoveOutOfRecord(camera);
            }
        }
        diagnostics.RecordAccepted("deconstruct", mapRoom, sessionId: context.Sender.SessionId, reason: "locks_reconciled");
    }

    private bool IsRegisteredWithAnotherLiveRoom(MapRoomEntity deconstructingRoom, NitroxId cameraId)
    {
        foreach (MapRoomEntity room in entityRegistry.GetEntities<MapRoomEntity>())
        {
            if (ReferenceEquals(room, deconstructingRoom) || room.Id == deconstructingRoom.Id)
            {
                continue;
            }
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
}
