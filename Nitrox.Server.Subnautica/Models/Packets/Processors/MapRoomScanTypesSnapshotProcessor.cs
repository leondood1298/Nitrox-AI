using System.Collections.Generic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomScanTypesSnapshotProcessor(EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData, ScannerRoomDiagnostics diagnostics) : IAuthPacketProcessor<MapRoomScanTypesSnapshot>
{
    public async Task Process(AuthProcessorContext context, MapRoomScanTypesSnapshot packet)
    {
        if (!entityRegistry.TryGetEntityById(packet.MapRoomId, out MapRoomEntity room))
        {
            diagnostics.RecordRejected("scan_types", sessionId: context.Sender.SessionId, reason: "unknown_room");
            await context.ReplyAsync(new MapRoomScanTypesSnapshot(packet.MapRoomId, [], 0, true, false));
            return;
        }
        if (simulationOwnershipData.GetPlayerForLock(packet.MapRoomId) != context.Sender)
        {
            diagnostics.RecordRejected("scan_types", room, sessionId: context.Sender.SessionId, reason: "non_owner");
            await context.ReplyAsync(new MapRoomScanTypesSnapshot(packet.MapRoomId, [], 0, true, false));
            return;
        }
        if (!MapRoomScanTypesAuthority.TryApply(room, packet, out List<NitroxTechType> accepted, out long revision))
        {
            diagnostics.RecordRejected("scan_types", room, sessionId: context.Sender.SessionId, reason: "stale_or_invalid");
            await context.ReplyAsync(new MapRoomScanTypesSnapshot(packet.MapRoomId, [], 0, true, false));
            return;
        }
        diagnostics.RecordAccepted("scan_types", room, sessionId: context.Sender.SessionId, reason: $"types_{accepted.Count}");
        await context.SendToAllAsync(new MapRoomScanTypesSnapshot(packet.MapRoomId, accepted, revision, true, true));
    }
}
