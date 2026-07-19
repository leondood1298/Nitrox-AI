using Nitrox.Model.Packets.Core;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class MapRoomCameraPreviewProcessor(MapRoomCameras mapRoomCameras) :
    IClientPacketProcessor<MapRoomCameraPreview>
{
    public Task Process(ClientProcessorContext context, MapRoomCameraPreview packet)
    {
        mapRoomCameras.ProcessPreview(packet);
        return Task.CompletedTask;
    }
}
