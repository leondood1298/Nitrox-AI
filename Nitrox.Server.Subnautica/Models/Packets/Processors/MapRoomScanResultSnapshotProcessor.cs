using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomScanResultSnapshotProcessor(ScannerRoomManager scannerRoomManager) : IAuthPacketProcessor<MapRoomScanResultSnapshot>
{
    public async Task Process(AuthProcessorContext context, MapRoomScanResultSnapshot packet)
    {
        await scannerRoomManager.ReportScanAsync(context.Sender, packet);
    }
}
