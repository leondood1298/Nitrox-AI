using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class StalkerFedByPlayerProcessor(StalkerFriendships stalkerFriendships) : IClientPacketProcessor<StalkerFedByPlayer>
{
    private readonly StalkerFriendships stalkerFriendships = stalkerFriendships;

    public Task Process(ClientProcessorContext context, StalkerFedByPlayer packet)
    {
        stalkerFriendships.ApplyFed(packet);
        return Task.CompletedTask;
    }
}
