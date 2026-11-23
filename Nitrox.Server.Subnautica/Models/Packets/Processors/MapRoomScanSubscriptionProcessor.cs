using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomScanSubscriptionProcessor(ScannerRoomManager scannerRoomManager) : IAuthPacketProcessor<MapRoomScanSubscription>
{
    public async Task Process(AuthProcessorContext context, MapRoomScanSubscription packet)
    {
        await scannerRoomManager.SetSubscriptionAsync(context.Sender, packet.MapRoomId, packet.Subscribed);
    }
}
