using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomScanResultReportProcessor(ScannerRoomManager scannerRoomManager) : IAuthPacketProcessor<MapRoomScanResultReport>
{
    public async Task Process(AuthProcessorContext context, MapRoomScanResultReport packet)
    {
        await scannerRoomManager.ReportScanResultAsync(context.Sender, packet);
    }
}
