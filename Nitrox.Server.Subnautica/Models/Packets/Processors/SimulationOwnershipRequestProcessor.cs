using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class SimulationOwnershipRequestProcessor(SimulationOwnershipData simulationOwnershipData,
    EntitySimulation entitySimulation, EntityRegistry entityRegistry, ScannerRoomManager scannerRoomManager) : IAuthPacketProcessor<SimulationOwnershipRequest>
{
    private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;
    private readonly EntitySimulation entitySimulation = entitySimulation;
    private readonly EntityRegistry entityRegistry = entityRegistry;

    public async Task Process(AuthProcessorContext context, SimulationOwnershipRequest ownershipRequest)
    {
        // An exclusive lock on a Scanner Room camera that is held for a Stalker gives way to a player who asks.
        await scannerRoomManager.MakeWayForExclusiveRequestAsync(context.Sender, ownershipRequest);

        if (!entityRegistry.TryGetEntityById(ownershipRequest.Id, out WorldEntity worldEntity))
        {
            await ProcessNonWorldEntityRequest(context, ownershipRequest);
            return;
        }

        List<SimulatedEntity> simulatedEntities = [.. entitySimulation.TryAcquireEntity(context.Sender, worldEntity, true, ownershipRequest.LockType, explicitRequest: true)];

        if (simulatedEntities.Count == 0)
        {
            await context.ReplyAsync(new SimulationOwnershipResponse(ownershipRequest.Id, false, ownershipRequest.LockType));
            return;
        }

        await context.ReplyAsync(new SimulationOwnershipResponse(ownershipRequest.Id, true, ownershipRequest.LockType));
        await context.SendToAllAsync(new SimulationOwnershipChange(simulatedEntities));
    }

    /// <summary>
    ///     Players also take turns on things that are not world entities: the fabricator inside a Cyclops or a lifepod is
    ///     a child entity, for example. Those have no cell and nothing to propagate the lock to, so the lock is simply
    ///     granted or refused, as it was before world entities got their own path.
    /// </summary>
    private async Task ProcessNonWorldEntityRequest(AuthProcessorContext context, SimulationOwnershipRequest ownershipRequest)
    {
        bool acquiredLock = simulationOwnershipData.TryAcquire(ownershipRequest.Id, context.Sender, ownershipRequest.LockType);
        if (acquiredLock)
        {
            await context.SendToOthersAsync(new SimulationOwnershipChange(ownershipRequest.Id, context.Sender.SessionId, ownershipRequest.LockType));
        }
        await context.ReplyAsync(new SimulationOwnershipResponse(ownershipRequest.Id, acquiredLock, ownershipRequest.LockType));
    }
}
