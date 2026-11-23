using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class DroppedItemSmellProcessor(StalkerFriendships stalkerFriendships) : IClientPacketProcessor<DroppedItemSmell>
{
    private readonly StalkerFriendships stalkerFriendships = stalkerFriendships;

    public Task Process(ClientProcessorContext context, DroppedItemSmell packet)
    {
        stalkerFriendships.RememberSmell(packet);
        return Task.CompletedTask;
    }
}
