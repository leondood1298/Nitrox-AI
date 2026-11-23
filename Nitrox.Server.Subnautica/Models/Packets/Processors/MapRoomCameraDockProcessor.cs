using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomCameraDockProcessor(ScannerRoomManager scannerRoomManager) : IAuthPacketProcessor<MapRoomCameraDock>
{
    public async Task Process(AuthProcessorContext context, MapRoomCameraDock packet)
    {
        if (packet.IsDocked)
        {
            await scannerRoomManager.DockCameraAsync(context.Sender, packet);
        }
        else
        {
            await scannerRoomManager.UndockCameraAsync(context.Sender, packet);
        }
    }
}
