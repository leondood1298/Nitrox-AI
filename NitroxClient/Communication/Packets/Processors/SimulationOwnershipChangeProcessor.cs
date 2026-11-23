using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class SimulationOwnershipChangeProcessor(SimulationOwnership simulationOwnershipManager, MapRoomCameras mapRoomCameras) : IClientPacketProcessor<SimulationOwnershipChange>
{
    private readonly SimulationOwnership simulationOwnershipManager = simulationOwnershipManager;
    private readonly MapRoomCameras mapRoomCameras = mapRoomCameras;

    public Task Process(ClientProcessorContext context, SimulationOwnershipChange simulationOwnershipChange)
    {
        foreach (SimulatedEntity simulatedEntity in simulationOwnershipChange.Entities)
        {
            bool lockLost = simulationOwnershipManager.HasAnyLockType(simulatedEntity.Id) && !simulationOwnershipManager.IsLocalPlayer(simulatedEntity.SessionId);
            simulationOwnershipManager.TreatSimulatedEntity(simulatedEntity);
            if (lockLost)
            {
                // A Scanner Room camera is controlled with its lock. Without the lock the player cannot stay in its view.
                mapRoomCameras.LockLost(simulatedEntity.Id);
            }
        }
        return Task.CompletedTask;
    }
}
