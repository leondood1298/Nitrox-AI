using System.Collections.Generic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomScanResultSnapshotProcessor(EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData) : IAuthPacketProcessor<MapRoomScanResultSnapshot>
{
    public async Task Process(AuthProcessorContext context, MapRoomScanResultSnapshot packet)
    {
        if (!entityRegistry.TryGetEntityById(packet.MapRoomId, out MapRoomEntity room) || simulationOwnershipData.GetPlayerForLock(packet.MapRoomId) != context.Sender || !MapRoomScanResultAuthority.TryApplySnapshot(room, packet, out List<MapRoomScanResultRecord> results, out long revision))
        {
            await context.ReplyAsync(new MapRoomScanResultSnapshot(packet.MapRoomId, packet.Generation, [], 0, true, false));
            return;
        }
        await context.SendToAllAsync(new MapRoomScanResultSnapshot(packet.MapRoomId, packet.Generation, results, revision, true, true));
    }
}
