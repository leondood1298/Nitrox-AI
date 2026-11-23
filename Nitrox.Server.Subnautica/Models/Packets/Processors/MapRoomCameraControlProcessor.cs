using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomCameraControlProcessor(ScannerRoomManager scannerRoomManager) : IAuthPacketProcessor<MapRoomCameraControl>
{
    public async Task Process(AuthProcessorContext context, MapRoomCameraControl packet)
    {
        await scannerRoomManager.SetControlAsync(context.Sender, packet);
    }
}
