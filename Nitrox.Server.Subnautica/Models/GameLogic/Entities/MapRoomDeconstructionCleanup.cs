using System.Linq;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

internal sealed class MapRoomDeconstructionCleanup(EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData,
    MapRoomCameraControlReleaseFactory cameraControlReleaseFactory, MapRoomScanResultSubscriptions subscriptions,
    MapRoomCameraControlLifecycle controlLifecycle, PlayerManager playerManager, ScannerRoomDiagnostics diagnostics)
{
    public async Task CleanupAsync(MapRoomEntity mapRoom, AuthProcessorContext context)
    {
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

        simulationOwnershipData.RevokeOwnerOfId(mapRoom.Id);
        await context.SendToAllAsync(new DropSimulationOwnership(mapRoom.Id));
        MapRoomCameraRecord[] cameras;
        lock (mapRoom)
        {
            cameras = mapRoom.CameraRegistry.ToArray();
        }
        foreach (MapRoomCameraRecord camera in cameras)
        {
            using IDisposable lifecycleGate = await controlLifecycle.EnterAsync(camera.CameraId);
            if (IsRegisteredWithAnotherLiveRoom(mapRoom, camera.CameraId))
            {
                continue;
            }
            if (simulationOwnershipData.RevokeOwnerOfId(camera.CameraId, out SimulationOwnershipData.PlayerLock revokedLock) &&
                cameraControlReleaseFactory.TryCreate(camera.CameraId, revokedLock, "deconstruct", out MapRoomCameraControl release))
            {
                await context.SendToAllAsync(release);
            }
            await context.SendToAllAsync(new DropSimulationOwnership(camera.CameraId));
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
