using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomScanTypesReportProcessor(ScannerRoomManager scannerRoomManager) : IAuthPacketProcessor<MapRoomScanTypesReport>
{
    public async Task Process(AuthProcessorContext context, MapRoomScanTypesReport packet)
    {
        await scannerRoomManager.ReportScanTypesAsync(context.Sender, packet);
    }
}
