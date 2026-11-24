using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class EntityRepulsedProcessor(Repulsions repulsions) : IClientPacketProcessor<EntityRepulsed>
{
    private readonly Repulsions repulsions = repulsions;

    public Task Process(ClientProcessorContext context, EntityRepulsed packet)
    {
        repulsions.Repulsed(packet.Id, packet.Velocity.ToUnity());
        return Task.CompletedTask;
    }
}
