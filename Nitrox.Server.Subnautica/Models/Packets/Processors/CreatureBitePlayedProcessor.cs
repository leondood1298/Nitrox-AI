using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Models.Packets.Processors.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Passes a creature's bite to the players who see the creature.
/// </summary>
internal sealed class CreatureBitePlayedProcessor(PlayerManager playerManager, EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData)
    : TransmitIfCanSeePacketProcessor<CreatureBitePlayed>(playerManager, entityRegistry)
{
    private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;

    public override async Task Process(AuthProcessorContext context, CreatureBitePlayed packet)
    {
        // Only the simulating player's game decides that the creature bit
        if (!CreatureMetadataAuthority.IsFromSimulatingPlayer(simulationOwnershipData.GetPlayerForLock(packet.CreatureId), context.Sender))
        {
            return;
        }
        await TransmitIfCanSeeEntitiesAsync(context, packet, [packet.CreatureId]);
    }
}
