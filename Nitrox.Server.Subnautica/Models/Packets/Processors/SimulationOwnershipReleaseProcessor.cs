using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Moves the simulation of an entity away from a player who reports being unable to simulate it. Without this the
///     entity stays frozen for everyone: a base power source would neither generate nor lose what others take from it.
/// </summary>
internal sealed class SimulationOwnershipReleaseProcessor(PlayerManager playerManager, EntitySimulation entitySimulation, EntityRegistry entityRegistry, BasePowerDiagnostics? basePowerDiagnostics = null)
    : IAuthPacketProcessor<SimulationOwnershipRelease>
{
    public async Task Process(AuthProcessorContext context, SimulationOwnershipRelease packet)
    {
        if (!entityRegistry.TryGetEntityById(packet.Id, out Entity entity))
        {
            return;
        }
        List<Player> otherPlayers = playerManager.GetConnectedPlayersExcept(context.Sender);
        if (!entitySimulation.TryReleaseBackgroundSimulation(context.Sender, entity, otherPlayers, out List<SimulatedEntity>? takenOver))
        {
            basePowerDiagnostics?.RecordHandBack(entity, context.Sender.SessionId, false, false);
            return;
        }
        basePowerDiagnostics?.RecordHandBack(entity, context.Sender.SessionId, true, takenOver != null);

        if (takenOver != null)
        {
            await entitySimulation.BroadcastSimulationChanges(takenOver);
        }
        else
        {
            // No player has taken simulation on the entity
            await context.SendToAllAsync(new DropSimulationOwnership([packet.Id]));
        }
    }
}
