using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class CreatureBitePlayedProcessor(CreatureBites creatureBites) : IClientPacketProcessor<CreatureBitePlayed>
{
    private readonly CreatureBites creatureBites = creatureBites;

    public Task Process(ClientProcessorContext context, CreatureBitePlayed packet)
    {
        creatureBites.Apply(packet);
        return Task.CompletedTask;
    }
}
