using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomCameraPreviewProcessor(ScannerRoomManager scannerRoomManager) : IAuthPacketProcessor<MapRoomCameraPreview>
{
    public async Task Process(AuthProcessorContext context, MapRoomCameraPreview packet)
    {
        await scannerRoomManager.PublishPreviewAsync(context.Sender, packet);
    }
}
