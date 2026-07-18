using Nitrox.Model.DataStructures;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class SimulationOwnershipRequestProcessor(SimulationOwnershipData simulationOwnershipData,
    EntitySimulation entitySimulation, MapRoomCameraControlReleaseFactory cameraControlReleaseFactory,
    MapRoomCameraControlLifecycle cameraControlLifecycle, ScannerRoomDiagnostics diagnostics) : IAuthPacketProcessor<SimulationOwnershipRequest>
{
    private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;
    private readonly EntitySimulation entitySimulation = entitySimulation;

    public async Task Process(AuthProcessorContext context, SimulationOwnershipRequest ownershipRequest)
    {
        bool hasScannerLifecycle = cameraControlReleaseFactory.IsScannerCamera(ownershipRequest.Id) ||
                                   cameraControlLifecycle.IsKnown(ownershipRequest.Id);
        using IDisposable? lifecycleGate = hasScannerLifecycle
            ? await cameraControlLifecycle.EnterAsync(ownershipRequest.Id)
            : null;
        if (hasScannerLifecycle && !cameraControlReleaseFactory.IsScannerCamera(ownershipRequest.Id))
        {
            await context.ReplyAsync(new SimulationOwnershipResponse(ownershipRequest.Id, false, ownershipRequest.LockType));
            diagnostics.RecordRejected("camera_lock", cameraId: ownershipRequest.Id,
                sessionId: context.Sender.SessionId, reason: "removed");
            return;
        }
        bool aquiredLock = simulationOwnershipData.TryToAcquire(ownershipRequest.Id, context.Sender, ownershipRequest.LockType);

        if (aquiredLock)
        {
            bool shouldEntityMove = entitySimulation.ShouldSimulateEntityMovement(ownershipRequest.Id);
            SimulationOwnershipChange simulationOwnershipChange = new(ownershipRequest.Id, context.Sender.SessionId, ownershipRequest.LockType, shouldEntityMove);
            await context.SendToOthersAsync(simulationOwnershipChange);
        }

        SimulationOwnershipResponse responseToPlayer = new(ownershipRequest.Id, aquiredLock, ownershipRequest.LockType);
        await context.ReplyAsync(responseToPlayer);
        if (hasScannerLifecycle)
        {
            string reason = ownershipRequest.LockType == SimulationLockType.EXCLUSIVE ? "exclusive" : "transient";
            if (aquiredLock)
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
}
