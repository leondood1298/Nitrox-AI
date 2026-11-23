using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomCameraControlChangedProcessor(ScannerRoomManager scannerRoomManager) : IAuthPacketProcessor<MapRoomCameraControlChanged>
{
    public async Task Process(AuthProcessorContext context, MapRoomCameraControlChanged packet)
    {
        await scannerRoomManager.SetControlAsync(context.Sender, packet);
    }
}
