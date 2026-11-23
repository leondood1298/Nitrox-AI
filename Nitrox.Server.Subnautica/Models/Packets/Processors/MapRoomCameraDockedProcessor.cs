using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomCameraDockingProcessor(ScannerRoomManager scannerRoomManager) : IAuthPacketProcessor<MapRoomCameraDocked>
{
    public async Task Process(AuthProcessorContext context, MapRoomCameraDocked packet)
    {
        await scannerRoomManager.DockCameraAsync(context.Sender, packet);
    }
}
