using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomScanResultSnapshotProcessor(EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData, PlayerManager playerManager, MapRoomScanResultSubscriptions subscriptions, ScannerRoomDiagnostics diagnostics) : IAuthPacketProcessor<MapRoomScanResultSnapshot>
{
    public async Task Process(AuthProcessorContext context, MapRoomScanResultSnapshot packet)
    {
        if (!entityRegistry.TryGetEntityById(packet.MapRoomId, out MapRoomEntity room))
        {
            diagnostics.RecordRejected("scan_snapshot", sessionId: context.Sender.SessionId, reason: "unknown_room");
            await context.ReplyAsync(new MapRoomScanResultSnapshot(packet.MapRoomId, packet.Generation, [], 0, true, false));
            return;
        }
        if (simulationOwnershipData.GetPlayerForLock(packet.MapRoomId) != context.Sender)
        {
            diagnostics.RecordRejected("scan_snapshot", room, sessionId: context.Sender.SessionId, reason: "non_owner");
            await context.ReplyAsync(new MapRoomScanResultSnapshot(packet.MapRoomId, packet.Generation, [], 0, true, false));
            return;
        }
        if (!MapRoomScanResultAuthority.TryApplySnapshot(room, packet, out List<MapRoomScanResultRecord> results, out long revision))
        {
            diagnostics.RecordRejected("scan_snapshot", room, sessionId: context.Sender.SessionId, reason: "stale_or_invalid");
            await context.ReplyAsync(new MapRoomScanResultSnapshot(packet.MapRoomId, packet.Generation, [], 0, true, false));
            return;
        }
        MapRoomScanResultSnapshot response = new(packet.MapRoomId, packet.Generation, results, revision, true, true);
        diagnostics.RecordAccepted("scan_snapshot", room, sessionId: context.Sender.SessionId, reason: $"results_{results.Count}");
        await context.ReplyAsync(response);
        foreach (Player player in playerManager.GetConnectedPlayersExcept(context.Sender).Where(player => subscriptions.Contains(room.Id, player.SessionId)))
        {
            await context.SendAsync(response, player.SessionId);
        }
    }
}
