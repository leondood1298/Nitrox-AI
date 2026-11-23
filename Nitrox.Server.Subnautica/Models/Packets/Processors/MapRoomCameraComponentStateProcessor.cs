using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomCameraComponentStateProcessor(ScannerRoomManager scannerRoomManager) : IAuthPacketProcessor<MapRoomCameraComponentState>
{
    public async Task Process(AuthProcessorContext context, MapRoomCameraComponentState packet)
    {
        await scannerRoomManager.SetCameraComponentsAsync(context.Sender, packet);
    }
}
