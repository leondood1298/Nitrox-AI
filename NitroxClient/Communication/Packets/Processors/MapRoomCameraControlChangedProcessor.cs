using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class MapRoomCameraControlChangedProcessor(MapRoomCameras mapRoomCameras) : IClientPacketProcessor<MapRoomCameraControlChanged>
{
    public Task Process(ClientProcessorContext context, MapRoomCameraControlChanged packet)
    {
        mapRoomCameras.ProcessControlChanged(packet);
        return Task.CompletedTask;
    }
}
