using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomCameraLightProcessor(ScannerRoomManager scannerRoomManager) : IAuthPacketProcessor<MapRoomCameraLight>
{
    public async Task Process(AuthProcessorContext context, MapRoomCameraLight packet)
    {
        await scannerRoomManager.SetCameraLightAsync(context.Sender, packet);
    }
}
