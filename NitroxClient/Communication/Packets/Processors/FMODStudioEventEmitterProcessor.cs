using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic.FMOD;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class FMODStudioEventEmitterProcessor(RemoteEntitySounds remoteEntitySounds) : IClientPacketProcessor<FMODStudioEmitterPacket>
{
    private readonly RemoteEntitySounds remoteEntitySounds = remoteEntitySounds;

    public Task Process(ClientProcessorContext context, FMODStudioEmitterPacket packet)
    {
        remoteEntitySounds.Apply(packet.Id, packet.AssetPath, packet.Play, packet.Looping, 1f, packet.AllowFadeout);
        return Task.CompletedTask;
    }
}
