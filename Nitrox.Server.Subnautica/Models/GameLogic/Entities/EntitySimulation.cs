using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.AppEvents;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

internal sealed class EntitySimulation : ISessionCleaner
{
    private const SimulationLockType DEFAULT_ENTITY_SIMULATION_LOCKTYPE = SimulationLockType.TRANSIENT;
    private readonly EntityRegistry entityRegistry;
    private readonly ILogger<EntitySimulation> logger;
    private readonly MapRoomCameraControlReleaseFactory cameraControlReleaseFactory;
    private readonly MapRoomCameraControlLifecycle cameraControlLifecycle;

    private readonly IPacketSender packetSender;
    private readonly PlayerManager playerManager;
    private readonly SimulationOwnershipData simulationOwnershipData;
    private readonly WorldEntityManager worldEntityManager;

    public EntitySimulation(IPacketSender packetSender, EntityRegistry entityRegistry, WorldEntityManager worldEntityManager,
        SimulationOwnershipData simulationOwnershipData, PlayerManager playerManager,
        MapRoomCameraControlReleaseFactory cameraControlReleaseFactory, MapRoomCameraControlLifecycle cameraControlLifecycle,
        ILogger<EntitySimulation> logger)
    {
        this.packetSender = packetSender;
        this.entityRegistry = entityRegistry;
        this.worldEntityManager = worldEntityManager;
        this.simulationOwnershipData = simulationOwnershipData;
        this.playerManager = playerManager;
        this.cameraControlReleaseFactory = cameraControlReleaseFactory;
        this.cameraControlLifecycle = cameraControlLifecycle;
        this.logger = logger;
    }

    public IEnumerable<SimulatedEntity> GetSimulationChangesForCell(Player player, AbsoluteEntityCell cell)
    {
        foreach (WorldEntity entity in GetPlayerSimulatedEntities(player, cell))
        {
            bool doesEntityMove = ShouldSimulateEntityMovement(entity);
            yield return new SimulatedEntity(entity.Id, player.SessionId, doesEntityMove, DEFAULT_ENTITY_SIMULATION_LOCKTYPE);
        }
    }

    public void FillWithRemovedCells(Player player, AbsoluteEntityCell removedCell, List<SimulatedEntity> ownershipChanges)
    {
        AssignEntitiesToOtherPlayers(player.SessionId, GetEntitiesToRevoke(player, removedCell), ownershipChanges);
        return;

        IEnumerable<WorldEntity> GetEntitiesOfCell(AbsoluteEntityCell cell)
        {
            foreach (WorldEntity entity in worldEntityManager.GetEntities(cell))
            {
                yield return entity;
                foreach (WorldEntity child in GetSimulatableChildren(entity))
                {
                    yield return child;
                }
            }
        }

        IEnumerable<WorldEntity> GetEntitiesToRevoke(Player simulatingPlayer, AbsoluteEntityCell cell)
        {
            foreach (WorldEntity entity in GetEntitiesOfCell(cell))
            {
                if (player.CanSee(entity))
                {
                    continue;
                }
                if (simulationOwnershipData.TryGetLock(entity.Id, out SimulationOwnershipData.PlayerLock currentLock) &&
                    currentLock.Player == simulatingPlayer && currentLock.LockType == SimulationLockType.EXCLUSIVE &&
                    cameraControlReleaseFactory.IsScannerCamera(entity.Id))
                {
                    continue;
                }
                if (!simulationOwnershipData.RevokeIfOwner(entity.Id, simulatingPlayer))
                {
                    continue;
                }

                yield return entity;
            }
        }
    }

    private IEnumerable<WorldEntity> GetSimulatableChildren(WorldEntity entity)
    {
        return entity.ChildEntities.OfType<WorldEntity>().Where(ShouldSimulateEntity);
    }

    private IEnumerable<WorldEntity> GetPlayerSimulatedEntities(Player simulatingPlayer, AbsoluteEntityCell cell)
    {
        foreach (WorldEntity entity in worldEntityManager.GetEntities(cell))
        {
            if (!simulatingPlayer.CanSee(entity))
            {
                continue;
            }
            if (!ShouldSimulateEntity(entity))
            {
                continue;
            }
            if (!simulationOwnershipData.TryToAcquire(entity.Id, simulatingPlayer, DEFAULT_ENTITY_SIMULATION_LOCKTYPE))
            {
                continue;
            }

            yield return entity;
            foreach (WorldEntity child in GetSimulatableChildren(entity))
            {
                if (simulationOwnershipData.TryToAcquire(child.Id, simulatingPlayer, DEFAULT_ENTITY_SIMULATION_LOCKTYPE))
                {
                    yield return child;
                }
            }
        }
    }

    public void BroadcastSimulationChanges(List<SimulatedEntity> ownershipChanges)
    {
        if (ownershipChanges.Count > 0)
        {
            SimulationOwnershipChange ownershipChange = new(ownershipChanges);
            packetSender.SendPacketToAllAsync(ownershipChange);
        }
    }

    public bool TryAssignEntityToPlayer(Entity entity, Player player, bool shouldEntityMove, [NotNullWhen(true)] out SimulatedEntity? simulatedEntity)
    {
        if (simulationOwnershipData.TryToAcquire(entity.Id, player, DEFAULT_ENTITY_SIMULATION_LOCKTYPE))
        {
            bool doesEntityMove = shouldEntityMove && entity is WorldEntity worldEntity && ShouldSimulateEntityMovement(worldEntity);
            simulatedEntity = new(entity.Id, player.SessionId, doesEntityMove, DEFAULT_ENTITY_SIMULATION_LOCKTYPE);
            return true;
        }

        simulatedEntity = null;
        return false;
    }

    public List<SimulatedEntity> AssignGlobalRootEntitiesAndGetData(Player player)
    {
        List<SimulatedEntity> simulatedEntities = new();
        foreach (GlobalRootEntity entity in worldEntityManager.GetInitialSyncGlobalRootEntities())
        {
            simulationOwnershipData.TryToAcquire(entity.Id, player, SimulationLockType.TRANSIENT);
            if (!simulationOwnershipData.TryGetLock(entity.Id, out SimulationOwnershipData.PlayerLock playerLock))
            {
                continue;
            }
            bool doesEntityMove = ShouldSimulateEntityMovement(entity);
            SimulatedEntity simulatedEntity = new(entity.Id, playerLock.Player.SessionId, doesEntityMove, playerLock.LockType);
            simulatedEntities.Add(simulatedEntity);
        }
        return simulatedEntities;
    }

    public bool TryAssignEntityToPlayers(List<Player> players, Entity entity, [NotNullWhen(true)] out SimulatedEntity? simulatedEntity)
    {
        NitroxId id = entity.Id;

        foreach (Player player in players)
        {
            if (player.CanSee(entity) && simulationOwnershipData.TryToAcquire(id, player, DEFAULT_ENTITY_SIMULATION_LOCKTYPE))
            {
                bool doesEntityMove = entity is WorldEntity worldEntity && ShouldSimulateEntityMovement(worldEntity);

                logger.ZLogTrace($"Player {player.Name} has taken over simulating {id}");
                simulatedEntity = new(id, player.SessionId, doesEntityMove, DEFAULT_ENTITY_SIMULATION_LOCKTYPE);
                return true;
            }
        }

        simulatedEntity = null;
        return false;
    }

    public bool ShouldSimulateEntity(WorldEntity entity)
    {
        return SimulationWhitelist.UtilityWhitelist.Contains(entity.TechType) || ShouldSimulateEntityMovement(entity);
    }

    public bool ShouldSimulateEntityMovement(WorldEntity entity)
    {
        return !entity.SpawnedByServer || SimulationWhitelist.MovementWhitelist.Contains(entity.TechType);
    }

    public bool ShouldSimulateEntityMovement(NitroxId entityId)
    {
        return entityRegistry.TryGetEntityById(entityId, out WorldEntity worldEntity) && ShouldSimulateEntityMovement(worldEntity);
    }

    public async Task EntityDestroyedAsync(NitroxId id)
    {
        using IDisposable? lifecycleGate = cameraControlReleaseFactory.IsScannerCamera(id)
            ? await cameraControlLifecycle.EnterAsync(id)
            : null;
        await EntityDestroyedWithLifecycleGateAsync(id);
    }

    internal async Task EntityDestroyedWithLifecycleGateAsync(NitroxId id)
    {
        if (simulationOwnershipData.RevokeOwnerOfId(id, out SimulationOwnershipData.PlayerLock revokedLock) &&
            cameraControlReleaseFactory.TryCreate(id, revokedLock, "destroy", out MapRoomCameraControl release))
        {
            await packetSender.SendPacketToAllAsync(release);
        }
    }

    public async Task OnEventAsync(ISessionCleaner.Args args)
    {
        IReadOnlyList<IDisposable> lifecycleGates = await cameraControlLifecycle.EnterManyAsync(
            cameraControlReleaseFactory.GetScannerCameraIds());
        try
        {
            List<SimulationOwnershipData.RevokedLock> revokedLocks = simulationOwnershipData.RevokeAllLocksForOwner(args.Session.Id);
            List<NitroxId> revokedEntityIds = revokedLocks.Select(revoked => revoked.EntityId).ToList();
            List<Entity> revokedEntities = entityRegistry.GetEntities(revokedEntityIds);
            List<NitroxId> unregisteredRevokedIds = revokedEntityIds.Except(revokedEntities.Select(entity => entity.Id)).ToList();

            foreach (SimulationOwnershipData.RevokedLock revokedLock in revokedLocks)
            {
                if (cameraControlReleaseFactory.TryCreate(revokedLock.EntityId, revokedLock.Lock, "disconnect", out MapRoomCameraControl release))
                {
                    await packetSender.SendPacketToAllAsync(release);
                }
            }

            // Release control first; only then may another player receive ordinary simulation.
            List<SimulatedEntity> ownershipChanges = [];
            AssignEntitiesToOtherPlayers(args.Session.Id, revokedEntities, ownershipChanges);
            if (ownershipChanges.Count > 0)
            {
                await packetSender.SendPacketToAllAsync(new SimulationOwnershipChange(ownershipChanges));
            }
            foreach (NitroxId revokedId in unregisteredRevokedIds)
            {
                logger.ZLogInformation($"Dropping unregistered simulation lock {revokedId} after session {args.Session.Id} disconnected");
                await packetSender.SendPacketToAllAsync(new DropSimulationOwnership(revokedId));
            }
        }
        finally
        {
            MapRoomCameraControlLifecycle.ReleaseReverse(lifecycleGates);
        }

    }

    private void AssignEntitiesToOtherPlayers(SessionId oldSessionId, IEnumerable<Entity> entities, List<SimulatedEntity> ownershipChanges)
    {
        List<Player> otherPlayers = playerManager.GetConnectedPlayersExcept(oldSessionId);
        foreach (Entity entity in entities)
        {
            if (TryAssignEntityToPlayers(otherPlayers, entity, out SimulatedEntity simulatedEntity))
            {
                ownershipChanges.Add(simulatedEntity);
            }
        }
    }
}
