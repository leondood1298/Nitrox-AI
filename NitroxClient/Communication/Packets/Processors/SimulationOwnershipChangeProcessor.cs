using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class SimulationOwnershipChangeProcessor(
    SimulationOwnership simulationOwnershipManager,
    MapRoomCameras mapRoomCameras,
    StalkerCameraGrabs stalkerCameraGrabs,
    CreatureInfections creatureInfections,
    StalkerShinyGrabs? stalkerShinyGrabs = null,
    CreatureLeashes? creatureLeashes = null)
    : IClientPacketProcessor<SimulationOwnershipChange>
{
    private readonly SimulationOwnership simulationOwnershipManager = simulationOwnershipManager;
    private readonly MapRoomCameras mapRoomCameras = mapRoomCameras;
    private readonly CreatureInfections creatureInfections = creatureInfections;

    public Task Process(ClientProcessorContext context, SimulationOwnershipChange simulationOwnershipChange)
    {
        foreach (SimulatedEntity simulatedEntity in simulationOwnershipChange.Entities)
        {
            bool hadLock = simulationOwnershipManager.HasAnyLockType(simulatedEntity.Id);
            bool lockLost = hadLock && !simulationOwnershipManager.IsLocalPlayer(simulatedEntity.SessionId);
            simulationOwnershipManager.TreatSimulatedEntity(simulatedEntity);
            if (!hadLock && simulationOwnershipManager.HasAnyLockType(simulatedEntity.Id))
            {
                // The creature appeared long ago: its infection roll ran without the lock
                creatureInfections.RollIfUndecided(simulatedEntity.Id);
                // Its leash and size may never have been sent
                if (creatureLeashes != null && NitroxEntity.TryGetComponentFrom(simulatedEntity.Id, out Creature creature) && creature.isInitialized)
                {
                    creatureLeashes.OnStarted(creature);
                }
            }
            if (lockLost)
            {
                // A Scanner Room camera is controlled, and carried by a Stalker, with its lock. Without the lock the
                // player cannot stay in its view, and the Stalker lets go of it.
                mapRoomCameras.LockLost(simulatedEntity.Id);
                stalkerCameraGrabs.LockLost(simulatedEntity.Id);
                // So does a Stalker with anything else it carries
                stalkerShinyGrabs?.LockLost(simulatedEntity.Id);
            }
        }
        return Task.CompletedTask;
    }
}
