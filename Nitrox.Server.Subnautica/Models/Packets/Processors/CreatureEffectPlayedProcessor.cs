using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Models.Packets.Processors.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Passes what a creature did to the players who see it, when its simulating player sent it.
/// </summary>
internal sealed class CreatureEffectPlayedProcessor(PlayerManager playerManager, EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData)
    : TransmitIfCanSeePacketProcessor<CreatureEffectPlayed>(playerManager, entityRegistry)
{
    private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;

    public override async Task Process(AuthProcessorContext context, CreatureEffectPlayed packet)
    {
        if (!CreatureMetadataAuthority.IsFromSimulatingPlayer(simulationOwnershipData.GetPlayerForLock(packet.CreatureId), context.Sender))
        {
            return;
        }
        await TransmitIfCanSeeEntitiesAsync(context, packet, [packet.CreatureId]);
    }
}
