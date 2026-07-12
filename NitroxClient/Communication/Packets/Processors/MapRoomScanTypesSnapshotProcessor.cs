using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class MapRoomScanTypesSnapshotProcessor : IClientPacketProcessor<MapRoomScanTypesSnapshot>
{
    public Task Process(ClientProcessorContext context, MapRoomScanTypesSnapshot packet)
    {
        MapRoomScanTypes.ProcessSnapshot(packet);
        return Task.CompletedTask;
    }
}
