using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class MapRoomCameraComponentStateProcessor(MapRoomCameras mapRoomCameras) : IClientPacketProcessor<MapRoomCameraComponentState>
{
    public Task Process(ClientProcessorContext context, MapRoomCameraComponentState packet)
    {
        mapRoomCameras.ProcessComponentState(packet);
        return Task.CompletedTask;
    }
}
