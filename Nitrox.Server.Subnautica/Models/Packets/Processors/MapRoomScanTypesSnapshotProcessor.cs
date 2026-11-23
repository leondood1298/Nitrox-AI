using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomScanTypesSnapshotProcessor(ScannerRoomManager scannerRoomManager) : IAuthPacketProcessor<MapRoomScanTypesSnapshot>
{
    public async Task Process(AuthProcessorContext context, MapRoomScanTypesSnapshot packet)
    {
        await scannerRoomManager.ReportScanTypesAsync(context.Sender, packet);
    }
}
