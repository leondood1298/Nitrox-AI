using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class CreatureAttackedPlayerProcessor(CreatureAttacksOnPlayers creatureAttacksOnPlayers) : IClientPacketProcessor<CreatureAttackedPlayer>
{
    private readonly CreatureAttacksOnPlayers creatureAttacksOnPlayers = creatureAttacksOnPlayers;

    public Task Process(ClientProcessorContext context, CreatureAttackedPlayer packet)
    {
        creatureAttacksOnPlayers.ApplyReported(packet);
        return Task.CompletedTask;
    }
}
