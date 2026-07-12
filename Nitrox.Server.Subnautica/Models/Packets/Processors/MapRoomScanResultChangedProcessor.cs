using System.Linq;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomScanResultChangedProcessor(EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData, PlayerManager playerManager, MapRoomScanResultSubscriptions subscriptions) : IAuthPacketProcessor<MapRoomScanResultChanged>
{
    public async Task Process(AuthProcessorContext context, MapRoomScanResultChanged packet)
    {
        if (!entityRegistry.TryGetEntityById(packet.MapRoomId, out MapRoomEntity room) || simulationOwnershipData.GetPlayerForLock(packet.MapRoomId) != context.Sender || !MapRoomScanResultAuthority.TryApply(room, packet))
        {
            await context.ReplyAsync(CreateResponse(packet, room: null, granted: false));
            return;
        }
        MapRoomScanResultChanged response = CreateResponse(packet, room, granted: true);
        await context.ReplyAsync(response);
        foreach (Player player in playerManager.GetConnectedPlayersExcept(context.Sender).Where(player => subscriptions.Contains(room.Id, player.SessionId)))
        {
            await context.SendAsync(response, player.SessionId);
        }
    }

    private static MapRoomScanResultChanged CreateResponse(MapRoomScanResultChanged packet, MapRoomEntity? room, bool granted) =>
        new(packet.MapRoomId, packet.Generation, packet.ResourceId, packet.TechType, packet.Position, packet.Removed, room?.ScanResultRevision ?? 0, true, granted);
}
