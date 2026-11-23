using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class MapRoomCameraUndockedProcessor(MapRoomCameraDocks mapRoomCameraDocks) : IClientPacketProcessor<MapRoomCameraUndocked>
{
    public Task Process(ClientProcessorContext context, MapRoomCameraUndocked packet)
    {
        mapRoomCameraDocks.ProcessUndocked(packet);
        return Task.CompletedTask;
    }
}
