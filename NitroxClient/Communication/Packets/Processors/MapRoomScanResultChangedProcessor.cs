using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class MapRoomScanResultChangedProcessor(MapRoomScans mapRoomScans) : IClientPacketProcessor<MapRoomScanResultChanged>
{
    public Task Process(ClientProcessorContext context, MapRoomScanResultChanged packet)
    {
        mapRoomScans.ApplyChange(packet);
        return Task.CompletedTask;
    }
}
