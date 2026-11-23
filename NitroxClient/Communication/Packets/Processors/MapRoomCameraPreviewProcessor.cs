using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class MapRoomCameraPreviewProcessor(MapRoomCameraPreviews mapRoomCameraPreviews) : IClientPacketProcessor<MapRoomCameraPreview>
{
    public Task Process(ClientProcessorContext context, MapRoomCameraPreview packet)
    {
        mapRoomCameraPreviews.ProcessPreview(packet);
        return Task.CompletedTask;
    }
}
