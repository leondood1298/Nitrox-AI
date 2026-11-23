using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class CreatureEffectPlayedProcessor(CreatureEffects creatureEffects) : IClientPacketProcessor<CreatureEffectPlayed>
{
    private readonly CreatureEffects creatureEffects = creatureEffects;

    public Task Process(ClientProcessorContext context, CreatureEffectPlayed packet)
    {
        creatureEffects.Apply(packet);
        return Task.CompletedTask;
    }
}
