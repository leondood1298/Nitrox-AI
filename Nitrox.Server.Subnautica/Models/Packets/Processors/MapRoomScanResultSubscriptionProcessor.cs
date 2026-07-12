using System.Linq;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomScanResultSubscriptionProcessor(EntityRegistry entityRegistry, MapRoomScanResultSubscriptions subscriptions) : IAuthPacketProcessor<MapRoomScanResultSubscription>
{
    public async Task Process(AuthProcessorContext context, MapRoomScanResultSubscription packet)
    {
        if (!entityRegistry.TryGetEntityById(packet.MapRoomId, out MapRoomEntity room))
        {
            return;
        }
        subscriptions.Set(packet.MapRoomId, context.Sender.SessionId, packet.Subscribed);
        if (!packet.Subscribed)
        {
            return;
        }
        await context.ReplyAsync(CreateSnapshot(room));
    }

    private static MapRoomScanResultSnapshot CreateSnapshot(MapRoomEntity room)
    {
        lock (room)
        {
            return new MapRoomScanResultSnapshot(room.Id, room.ScanResultGeneration,
                room.ScanResults.Select(result => new MapRoomScanResultRecord(result.ResourceId, result.TechType, result.Position)).ToList(),
                room.ScanResultRevision, true, true);
        }
    }
}
