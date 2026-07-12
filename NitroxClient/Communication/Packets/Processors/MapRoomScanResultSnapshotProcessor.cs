using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class MapRoomScanResultSnapshotProcessor : IClientPacketProcessor<MapRoomScanResultSnapshot>
{
    public Task Process(ClientProcessorContext context, MapRoomScanResultSnapshot packet)
    {
        MapRoomScanResults.ProcessSnapshot(packet);
        return Task.CompletedTask;
    }
}
