using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic.FMOD;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class FMODEventInstanceProcessor(RemoteEntitySounds remoteEntitySounds) : IClientPacketProcessor<FMODEventInstancePacket>
{
    private readonly RemoteEntitySounds remoteEntitySounds = remoteEntitySounds;

    public Task Process(ClientProcessorContext context, FMODEventInstancePacket packet)
    {
        remoteEntitySounds.Apply(packet.Id, packet.AssetPath, packet.Play, packet.Looping, packet.Volume, false);
        return Task.CompletedTask;
    }
}
