using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic.FMOD;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class FMODActiveLoopsProcessor(RemoteEntitySounds remoteEntitySounds) : IClientPacketProcessor<FMODActiveLoops>
{
    private readonly RemoteEntitySounds remoteEntitySounds = remoteEntitySounds;

    public Task Process(ClientProcessorContext context, FMODActiveLoops packet)
    {
        foreach (FMODActiveLoops.Loop loop in packet.Loops)
        {
            remoteEntitySounds.Apply(loop.Id, loop.AssetPath, true, true, loop.Volume, false);
        }
        return Task.CompletedTask;
    }
}
