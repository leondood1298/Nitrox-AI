using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomScanResultChangedProcessor(ScannerRoomManager scannerRoomManager) : IAuthPacketProcessor<MapRoomScanResultChanged>
{
    public async Task Process(AuthProcessorContext context, MapRoomScanResultChanged packet)
    {
        await scannerRoomManager.ReportScanResultAsync(context.Sender, packet);
    }
}
