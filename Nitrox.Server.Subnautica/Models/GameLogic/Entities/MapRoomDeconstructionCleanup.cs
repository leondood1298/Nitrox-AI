using System.Linq;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

internal sealed class MapRoomDeconstructionCleanup(SimulationOwnershipData simulationOwnershipData, MapRoomScanResultSubscriptions subscriptions, PlayerManager playerManager)
{
    public async Task CleanupAsync(MapRoomEntity mapRoom, AuthProcessorContext context)
    {
        MapRoomScanResultSnapshot clearedResults;
        lock (mapRoom)
        {
            mapRoom.BeginScanResultGeneration(mapRoom.ScanResultGeneration + 1);
            clearedResults = new MapRoomScanResultSnapshot(mapRoom.Id, mapRoom.ScanResultGeneration, [], mapRoom.ScanResultRevision, true, true);
        }
        var subscribedSessions = subscriptions.RemoveRoom(mapRoom.Id);
        foreach (Player player in playerManager.GetConnectedPlayers().Where(player => subscribedSessions.Contains(player.SessionId)))
        {
            await context.SendAsync(clearedResults, player.SessionId);
        }

        simulationOwnershipData.RevokeOwnerOfId(mapRoom.Id);
        await context.SendToAllAsync(new DropSimulationOwnership(mapRoom.Id));
        foreach (MapRoomCameraRecord camera in mapRoom.CameraRegistry)
        {
            simulationOwnershipData.RevokeOwnerOfId(camera.CameraId);
            await context.SendToAllAsync(new DropSimulationOwnership(camera.CameraId));
        }
    }
}
