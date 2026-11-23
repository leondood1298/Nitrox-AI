using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Helper;
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

    /// <returns>The successfully acquired entities from the provided <paramref name="cell"/>.</returns>
    public IEnumerable<SimulatedEntity> TryAcquireCellEntities(Player player, AbsoluteEntityCell cell)
    {
        return TryAcquireEntities(player, worldEntityManager.EnumerateCellEntities(cell), true, true);
    }

    private IEnumerable<SimulatedEntity> TryAcquireEntities(Player player, IEnumerable<Entity> entities, bool checkPlayerCanSee, bool propagate)
    {
        foreach (Entity entity in entities)
        {
            if (!SimulationWhitelist.ShouldSimulateEntity(entity))
            {
                continue;
            }

            if (checkPlayerCanSee && !player.CanSee(entity))
            {
                continue;
            }

            foreach (SimulatedEntity simulatedEntity in TryAcquireEntity(player, entity, propagate))
            {
                yield return simulatedEntity;
            }
        }
    }

    /// <param name="explicitRequest">
    ///     True when the player asked for exactly this lock type. Otherwise this is a refresh (a cell coming into view, a
    ///     reassignment), which must not weaken an exclusive lock the player already holds: they may be piloting a
    ///     vehicle or controlling a Scanner Room camera.
    /// </param>
    public IEnumerable<SimulatedEntity> TryAcquireEntity(Player player, Entity entity, bool propagate, SimulationLockType simulationLockType = DEFAULT_ENTITY_SIMULATION_LOCKTYPE, bool explicitRequest = false)
    {
        SimulationLockType acquiredLockType = simulationLockType;
        if (explicitRequest)
        {
            if (!simulationOwnershipData.TryAcquire(entity.Id, player, simulationLockType))
            {
                yield break;
            }
        }
        else
        {
            if (!simulationOwnershipData.TryAcquirePreservingExistingExclusive(entity.Id, player, simulationLockType, out SimulationOwnershipData.PlayerLock acquiredLock))
            {
                yield break;
            }
            acquiredLockType = acquiredLock.LockType;
        }

        bool doesEntityMove = entity is WorldEntity worldEntity && SimulationWhitelist.ShouldSimulateEntityMovement(worldEntity);
        yield return new SimulatedEntity(entity.Id, player.SessionId, doesEntityMove, acquiredLockType);

        if (propagate && SimulationWhitelist.ShouldPropagateSimulation(entity))
        {
            foreach (SimulatedEntity simulatedEntity in TryAcquireEntities(player, entity.ChildEntities, false, propagate))
            {
                yield return simulatedEntity;
            }
        }
    }

    /// <returns>The successfully revoked simulated entities from the provided <paramref name="cell"/>.</returns>
    public IEnumerable<WorldEntity> RevokeSimulatedCellEntities(Player simulatingPlayer, AbsoluteEntityCell cell)
    {
        return RevokeEntities(simulatingPlayer, worldEntityManager.EnumerateCellEntities(cell), true);
    }

    private IEnumerable<WorldEntity> RevokeEntities(Player simulatingPlayer, IEnumerable<WorldEntity> entities, bool checkCanPlayerSee)
    {
        foreach (WorldEntity worldEntity in entities)
        {
            if (checkCanPlayerSee && simulatingPlayer.CanSee(worldEntity))
            {
                continue;
            }
            // A Scanner Room camera being controlled stays with its controller; control ends through its own lifecycle
            if (!IsControlledScannerCamera(worldEntity.Id, simulatingPlayer) && simulationOwnershipData.RevokeIfOwner(worldEntity.Id, simulatingPlayer))
            {
                yield return worldEntity;
            }

            if (!SimulationWhitelist.ShouldPropagateSimulation(worldEntity))
            {
                continue;
            }

            // PlayerSeeOutOfCell state might remain in a tiny time frame while player unloads cell, so we propagate without checking CanSee
            foreach (WorldEntity childWorldEntity in RevokeEntities(simulatingPlayer, worldEntity.ChildEntities.OfType<WorldEntity>(), false))
            {
                yield return childWorldEntity;
            }
        }
    }

    /// <summary>
    /// Fills <paramref name="ownershipChanges"/> with ownership changes from revoking <paramref name="player"/>'s simulated entities in <paramref name="removedCell"/>.
    /// </summary>
    public void RevokeAndReassignCellEntities(Player player, AbsoluteEntityCell removedCell, List<SimulatedEntity> ownershipChanges)
    {
        AssignEntitiesToOtherPlayers(player.SessionId, RevokeSimulatedCellEntities(player, removedCell), ownershipChanges, true);
    }

    public async Task BroadcastSimulationChanges(List<SimulatedEntity> ownershipChanges)
    {
        if (ownershipChanges.Count > 0)
        {
            SimulationOwnershipChange ownershipChange = new(ownershipChanges);
            await packetSender.SendPacketToAllAsync(ownershipChange);
        }
    }

    /// <summary>
    /// Forcefully assign an entity to a player, revoking any previous ownership.
    /// </summary>
    public IEnumerable<SimulatedEntity> AssignEntityToPlayer(WorldEntity worldEntity, Player player, bool propagate)
    {
        // An exclusive lock held by someone else means they are piloting or controlling the entity right now; that is
        // not taken away. The player keeps an exclusive lock of their own.
        bool isLocked = simulationOwnershipData.TryGetLock(worldEntity.Id, out SimulationOwnershipData.PlayerLock currentLock);
        if (!isLocked || currentLock.LockType != SimulationLockType.EXCLUSIVE)
        {
            simulationOwnershipData.ForceAcquire(worldEntity.Id, player, DEFAULT_ENTITY_SIMULATION_LOCKTYPE);
        }
        return TryAcquireEntity(player, worldEntity, propagate);
    }

    public List<SimulatedEntity> AssignGlobalRootEntitiesAndGetData(Player player)
    {
        List<SimulatedEntity> simulatedEntities = [];
        foreach (GlobalRootEntity entity in worldEntityManager.GetInitialSyncGlobalRootEntities())
        {
            simulationOwnershipData.TryAcquirePreservingExistingExclusive(entity.Id, player, SimulationLockType.TRANSIENT, out _);
            if (!simulationOwnershipData.TryGetLock(entity.Id, out SimulationOwnershipData.PlayerLock playerLock))
            {
                continue;
            }
            bool doesEntityMove = SimulationWhitelist.ShouldSimulateEntityMovement(entity);
            SimulatedEntity simulatedEntity = new(entity.Id, playerLock.Player.SessionId, doesEntityMove, playerLock.LockType);
            simulatedEntities.Add(simulatedEntity);
        }
        return simulatedEntities;
    }

    public bool TryAssignEntityToPlayers(List<Player> players, Entity entity, bool propagate, [NotNullWhen(true)] out List<SimulatedEntity>? simulatedEntities)
    {
        NitroxId id = entity.Id;

        foreach (Player player in players)
        {
            if (!player.CanSee(entity))
            {
                continue;
            }

            simulatedEntities = [.. TryAcquireEntity(player, entity, false)];
            if (simulatedEntities.Count == 0)
            {
                continue;
            }

            if (propagate && SimulationWhitelist.ShouldPropagateSimulation(entity))
            {
                foreach (Entity childEntity in entity.ChildEntities)
                {
                    simulatedEntities.AddRange(TryAcquireEntity(player, childEntity, false));
                }
            }

            logger.ZLogTrace($"Player {player.Name} has taken over simulating {id}");
            return true;

        }

        simulatedEntities = null;
        return false;
    }

    public bool ShouldSimulateEntityMovement(NitroxId entityId)
    {
        return entityRegistry.TryGetEntityById(entityId, out WorldEntity worldEntity) && SimulationWhitelist.ShouldSimulateEntityMovement(worldEntity);
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
            AssignEntitiesToOtherPlayers(args.Session.Id, revokedEntities, ownershipChanges, false);
            await BroadcastSimulationChanges(ownershipChanges);

            // Locks on ids that are not registered entities (a docked Scanner Room camera, for example) cannot be
            // reassigned; players are told to forget them.
            if (unregisteredRevokedIds.Count > 0)
            {
                logger.ZLogInformation($"Dropping {unregisteredRevokedIds.Count} unregistered simulation lock(s) after session {args.Session.Id} disconnected");
                await packetSender.SendPacketToAllAsync(new DropSimulationOwnership(unregisteredRevokedIds));
            }
        }
        finally
        {
            MapRoomCameraControlLifecycle.ReleaseReverse(lifecycleGates);
        }
    }

    /// <summary>
    ///     Tells a player who has just joined which Scanner Room cameras are being controlled, and by whom. Control is
    ///     announced when it is granted, which they missed: such a camera would stay frozen for them while its controller
    ///     flies it, and their Scanner Room screen would offer it for control.
    /// </summary>
    public async Task SendCameraControlGrantsAsync(SessionId joinedSessionId)
    {
        IReadOnlyList<NitroxId> controlledCameraIds = cameraControlLifecycle.GetControlledCameras();
        if (controlledCameraIds.Count == 0)
        {
            return;
        }

        // A release keeps the camera's gate until its packet is queued. Holding the gates here keeps one from slipping
        // in between reading a grant and sending it, which would leave the joined player with a controller for good.
        using IDisposable lifecycleGates = await cameraControlLifecycle.EnterTogetherAsync(controlledCameraIds);
        foreach (NitroxId cameraId in controlledCameraIds)
        {
            if (simulationOwnershipData.TryGetLock(cameraId, out SimulationOwnershipData.PlayerLock controlLock) &&
                controlLock.LockType == SimulationLockType.EXCLUSIVE &&
                cameraControlLifecycle.IsActiveController(cameraId, controlLock.Player.SessionId) &&
                cameraControlReleaseFactory.TryCreateCurrentGrant(cameraId, controlLock.Player.SessionId, out MapRoomCameraControl grant))
            {
                await packetSender.SendPacketAsync(grant, joinedSessionId);
            }
        }
    }

    /// <summary>
    ///     Whether <paramref name="player" /> is controlling this Scanner Room camera right now. Control is only ended by
    ///     the camera's own lifecycle (release, pickup, docking, destruction, disconnect), never as a side effect of the
    ///     player losing sight of the camera or handing off its movement.
    /// </summary>
    internal bool IsControlledScannerCamera(NitroxId id, Player player)
    {
        return simulationOwnershipData.TryGetLock(id, out SimulationOwnershipData.PlayerLock currentLock) &&
               currentLock.Player.SessionId == player.SessionId && currentLock.LockType == SimulationLockType.EXCLUSIVE &&
               cameraControlReleaseFactory.IsScannerCamera(id);
    }

    public void AssignEntitiesToOtherPlayers(SessionId oldSessionId, IEnumerable<Entity> entities, List<SimulatedEntity> ownershipChanges, bool propagate)
    {
        // In case the enumerator can be counted (e.g. a list)
        if (entities.TryGetNonEnumeratedCount(out int count))
        {
            ownershipChanges.EnsureCapacity(ownershipChanges.Count + count);
        }

        // TODO: (optional) Find out if ordering the otherPlayers by distance to the previous simulator improves performance (ascending)
        List<Player> otherPlayers = playerManager.GetConnectedPlayersExcept(oldSessionId);
        foreach (Entity entity in entities)
        {
            if (TryAssignEntityToPlayers(otherPlayers, entity, propagate, out List<SimulatedEntity> simulatedEntities))
            {
                ownershipChanges.AddRange(simulatedEntities);
                continue;
            }

            ownershipChanges.Add(new(entity.Id, SessionId.SERVER_ID, false, SimulationLockType.TRANSIENT));
            if (propagate && SimulationWhitelist.ShouldPropagateSimulation(entity))
            {
                foreach (Entity childEntity in entity.ChildEntities)
                {
                    if (simulationOwnershipData.GetPlayerForLock(childEntity.Id) == null)
                    {
                        ownershipChanges.Add(new(childEntity.Id, SessionId.SERVER_ID, false, SimulationLockType.TRANSIENT));
                    }
                }
            }
        }
    }
}
