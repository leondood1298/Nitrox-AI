using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class MapRoomCameraDockedProcessor(MapRoomCameraDocks mapRoomCameraDocks) : IClientPacketProcessor<MapRoomCameraDocked>
{
    public Task Process(ClientProcessorContext context, MapRoomCameraDocked packet)
    {
        mapRoomCameraDocks.ProcessDocked(packet);
        return Task.CompletedTask;
    }
}
