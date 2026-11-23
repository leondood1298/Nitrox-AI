using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class MapRoomScanSnapshotProcessor(MapRoomScans mapRoomScans) : IClientPacketProcessor<MapRoomScanSnapshot>
{
    public Task Process(ClientProcessorContext context, MapRoomScanSnapshot packet)
    {
        mapRoomScans.ApplySnapshot(packet);
        return Task.CompletedTask;
    }
}
