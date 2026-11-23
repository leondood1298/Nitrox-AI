using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomScanResultSubscriptionProcessor(ScannerRoomManager scannerRoomManager) : IAuthPacketProcessor<MapRoomScanResultSubscription>
{
    public async Task Process(AuthProcessorContext context, MapRoomScanResultSubscription packet)
    {
        await scannerRoomManager.SetSubscriptionAsync(context.Sender, packet.MapRoomId, packet.Subscribed);
    }
}
