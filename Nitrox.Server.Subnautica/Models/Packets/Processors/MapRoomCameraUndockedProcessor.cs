using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomCameraUndockedProcessor(ScannerRoomManager scannerRoomManager) : IAuthPacketProcessor<MapRoomCameraUndocked>
{
    public async Task Process(AuthProcessorContext context, MapRoomCameraUndocked packet)
    {
        await scannerRoomManager.UndockCameraAsync(context.Sender, packet);
    }
}
