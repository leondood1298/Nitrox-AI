using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Models.Packets.Processors.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Passes a Sand Shark going under the sand to the players who see the shark.
/// </summary>
internal sealed class SandSharkCoverStartedProcessor(PlayerManager playerManager, EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData)
    : TransmitIfCanSeePacketProcessor<SandSharkCoverStarted>(playerManager, entityRegistry)
{
    private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;

    public override async Task Process(AuthProcessorContext context, SandSharkCoverStarted packet)
    {
        // Another player's copy of the shark does not burrow on its own; anything it sent would contradict the shark
        if (!CreatureMetadataAuthority.IsFromSimulatingPlayer(simulationOwnershipData.GetPlayerForLock(packet.CreatureId), context.Sender))
        {
            return;
        }
        await TransmitIfCanSeeEntitiesAsync(context, packet, [packet.CreatureId]);
    }
}
