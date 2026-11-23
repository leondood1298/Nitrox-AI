using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomScanReportProcessor(ScannerRoomManager scannerRoomManager) : IAuthPacketProcessor<MapRoomScanReport>
{
    public async Task Process(AuthProcessorContext context, MapRoomScanReport packet)
    {
        await scannerRoomManager.ReportScanAsync(context.Sender, packet);
    }
}
