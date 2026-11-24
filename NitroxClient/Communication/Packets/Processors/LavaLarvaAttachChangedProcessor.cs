using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class LavaLarvaAttachChangedProcessor(LavaLarvae lavaLarvae) : IClientPacketProcessor<LavaLarvaAttachChanged>
{
    private readonly LavaLarvae lavaLarvae = lavaLarvae;

    public Task Process(ClientProcessorContext context, LavaLarvaAttachChanged packet)
    {
        lavaLarvae.ApplyAttachment(packet);
        return Task.CompletedTask;
    }
}
