using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class MapRoomScanResultChangedProcessor : IClientPacketProcessor<MapRoomScanResultChanged>
{
    public Task Process(ClientProcessorContext context, MapRoomScanResultChanged packet)
    {
        MapRoomScanResults.ProcessDelta(packet);
        return Task.CompletedTask;
    }
}
