using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class MapRoomCameraControlProcessor(MapRoomCameras mapRoomCameras) : IClientPacketProcessor<MapRoomCameraControl>
{
    public Task Process(ClientProcessorContext context, MapRoomCameraControl packet)
    {
        mapRoomCameras.ProcessControl(packet);
        return Task.CompletedTask;
    }
}
