using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Hands a repulsion cannon's shot to the player whose game moves the object. An object nobody simulates becomes the
///     shooter's to simulate, so that its flight is seen by everybody.
/// </summary>
internal sealed class EntityRepulsedProcessor(SimulationOwnershipData simulationOwnershipData, EntityRegistry entityRegistry) : IAuthPacketProcessor<EntityRepulsed>
{
    private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;
    private readonly EntityRegistry entityRegistry = entityRegistry;

    public async Task Process(AuthProcessorContext context, EntityRepulsed packet)
    {
        Player? simulator = simulationOwnershipData.GetPlayerForLock(packet.Id);
        if (simulator == context.Sender)
        {
            return;
        }
        if (simulator != null)
        {
            await context.SendAsync(packet, simulator.SessionId);
            return;
        }
        if (entityRegistry.TryGetEntityById(packet.Id, out WorldEntity _) && simulationOwnershipData.TryAcquire(packet.Id, context.Sender, SimulationLockType.TRANSIENT))
        {
            await context.SendToAllAsync(new SimulationOwnershipChange(packet.Id, context.Sender.SessionId, SimulationLockType.TRANSIENT, true));
        }
    }
}
