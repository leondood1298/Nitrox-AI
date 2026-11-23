using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class SimulationOwnershipRequestProcessor(SimulationOwnershipData simulationOwnershipData,
    EntitySimulation entitySimulation, EntityRegistry entityRegistry, MapRoomCameraControlReleaseFactory cameraControlReleaseFactory,
    MapRoomCameraControlLifecycle cameraControlLifecycle, ScannerRoomDiagnostics diagnostics) : IAuthPacketProcessor<SimulationOwnershipRequest>
{
    private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;
    private readonly EntitySimulation entitySimulation = entitySimulation;
    private readonly EntityRegistry entityRegistry = entityRegistry;

    public async Task Process(AuthProcessorContext context, SimulationOwnershipRequest ownershipRequest)
    {
        bool hasScannerLifecycle = cameraControlReleaseFactory.IsScannerCamera(ownershipRequest.Id) ||
                                   cameraControlLifecycle.IsKnown(ownershipRequest.Id);
        if (hasScannerLifecycle)
        {
            await ProcessScannerCameraRequest(context, ownershipRequest);
            return;
        }

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

    /// <summary>
    ///     A Scanner Room camera is locked through its control lifecycle. A docked camera is a record of its room, not a
    ///     registered entity, so it cannot take the path above.
    /// </summary>
    private async Task ProcessScannerCameraRequest(AuthProcessorContext context, SimulationOwnershipRequest ownershipRequest)
    {
        using IDisposable lifecycleGate = await cameraControlLifecycle.EnterAsync(ownershipRequest.Id);
        if (!cameraControlReleaseFactory.IsScannerCamera(ownershipRequest.Id))
        {
            await context.ReplyAsync(new SimulationOwnershipResponse(ownershipRequest.Id, false, ownershipRequest.LockType));
            diagnostics.RecordRejected("camera_lock", cameraId: ownershipRequest.Id,
                sessionId: context.Sender.SessionId, reason: "removed");
            return;
        }
        bool requesterOwnsExclusive =
            simulationOwnershipData.TryGetLock(ownershipRequest.Id,
                out SimulationOwnershipData.PlayerLock currentLock) &&
            currentLock.Player == context.Sender &&
            currentLock.LockType == SimulationLockType.EXCLUSIVE;
        if (!cameraControlReleaseFactory.CanGrantGenericOwnership(
                ownershipRequest.Id, context.Sender.SessionId,
                ownershipRequest.LockType, requesterOwnsExclusive))
        {
            await context.ReplyAsync(new SimulationOwnershipResponse(
                ownershipRequest.Id, false, ownershipRequest.LockType));
            diagnostics.RecordRejected("camera_lock", cameraId: ownershipRequest.Id,
                sessionId: context.Sender.SessionId, reason: "control_required");
            return;
        }
        bool acquiredLock = simulationOwnershipData.TryAcquire(ownershipRequest.Id, context.Sender, ownershipRequest.LockType);
        if (acquiredLock && ownershipRequest.LockType != SimulationLockType.EXCLUSIVE)
        {
            cameraControlLifecycle.EndPreviewAcquisition(ownershipRequest.Id, context.Sender.SessionId);
        }

        if (acquiredLock)
        {
            bool shouldEntityMove = entitySimulation.ShouldSimulateEntityMovement(ownershipRequest.Id);
            SimulationOwnershipChange simulationOwnershipChange = new(ownershipRequest.Id, context.Sender.SessionId, ownershipRequest.LockType, shouldEntityMove);
            await context.SendToOthersAsync(simulationOwnershipChange);
        }

        await context.ReplyAsync(new SimulationOwnershipResponse(ownershipRequest.Id, acquiredLock, ownershipRequest.LockType));
        string reason = ownershipRequest.LockType == SimulationLockType.EXCLUSIVE ? "exclusive" : "transient";
        if (acquiredLock)
        {
            diagnostics.RecordAccepted("camera_lock", cameraId: ownershipRequest.Id,
                sessionId: context.Sender.SessionId, reason: reason);
        }
        else
        {
            diagnostics.RecordRejected("camera_lock", cameraId: ownershipRequest.Id,
                sessionId: context.Sender.SessionId, reason: reason);
        }
    }
}
