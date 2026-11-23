using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class MapRoomCameraDockProcessor(MapRoomCameras mapRoomCameras) : IClientPacketProcessor<MapRoomCameraDock>
{
    public Task Process(ClientProcessorContext context, MapRoomCameraDock packet)
    {
        mapRoomCameras.ProcessDock(packet);
        return Task.CompletedTask;
    }
}
