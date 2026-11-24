using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic.FMOD;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class FMODCustomEmitterProcessor(RemoteEntitySounds remoteEntitySounds) : IClientPacketProcessor<FMODCustomEmitterPacket>
{
    private readonly RemoteEntitySounds remoteEntitySounds = remoteEntitySounds;

    public Task Process(ClientProcessorContext context, FMODCustomEmitterPacket packet)
    {
        remoteEntitySounds.Apply(packet.Id, packet.AssetPath, packet.Play, packet.Looping, 1f, false);
        return Task.CompletedTask;
    }
}
