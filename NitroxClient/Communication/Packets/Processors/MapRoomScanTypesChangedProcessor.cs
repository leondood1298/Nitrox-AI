using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class MapRoomScanTypesChangedProcessor(MapRoomScans mapRoomScans) : IClientPacketProcessor<MapRoomScanTypesChanged>
{
    public Task Process(ClientProcessorContext context, MapRoomScanTypesChanged packet)
    {
        mapRoomScans.ApplyTypes(packet);
        return Task.CompletedTask;
    }
}
