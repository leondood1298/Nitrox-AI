using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.MultiplayerSession;
using Nitrox.Server.Subnautica.Models.AppEvents;
using Nitrox.Server.Subnautica.Models.Communication;
using Nitrox.Server.Subnautica.Models.GameLogic.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.GameLogic.Players;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

// TODO: Refactor this to a QueuingBackgroundService to simplify state tracking.
internal sealed class JoiningManager(
    IPacketSender packetSender,
    PlayerManager playerManager,
    SessionManager sessionManager,
    WorldEntityManager worldEntityManager,
    PdaManager pdaManager,
    StoryManager storyManager,
    StoryScheduler storyScheduler,
    EntitySimulation entitySimulation,
    ScannerRoomManager scannerRoomManager,
    EscapePodManager escapePodManager,
    EntityRegistry entityRegistry,
    SessionSettings sessionSettings,
    ActiveTeleporters activeTeleporters,
    CollectedTimeCapsules collectedTimeCapsules,
    IOptions<SubnauticaServerOptions> options,
    ILogger<JoiningManager> logger)
    : ISessionCleaner
{
    private static readonly TimeSpan SYNC_POLL_PERIOD = TimeSpan.FromMilliseconds(100);

    private readonly IPacketSender packetSender = packetSender;
    private readonly PlayerManager playerManager = playerManager;
    private readonly SessionManager sessionManager = sessionManager;
    private readonly WorldEntityManager worldEntityManager = worldEntityManager;
    private readonly PdaManager pdaManager = pdaManager;
    private readonly StoryManager storyManager = storyManager;
    private readonly StoryScheduler storyScheduler = storyScheduler;
    private readonly EntitySimulation entitySimulation = entitySimulation;
    private readonly IOptions<SubnauticaServerOptions> options = options;
    private readonly ILogger<JoiningManager> logger = logger;
    private readonly EscapePodManager escapePodManager = escapePodManager;
    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly SessionSettings sessionSettings = sessionSettings;
    private readonly ActiveTeleporters activeTeleporters = activeTeleporters;
    private readonly CollectedTimeCapsules collectedTimeCapsules = collectedTimeCapsules;

    private readonly ThreadSafeQueue<SessionId> joinQueue = new();
    private readonly Lock queueLocker = new(); // Necessary to avoid race conditions between JoinQueueLoop and AddToJoinQueue
    private bool queueActive;
    private readonly Lock syncLocker = new();
    private SessionId? syncingSession;
    private TaskCompletionSource? syncFinished;

    internal enum SyncOutcome
    {
        FINISHED,
        DISCONNECTED,
        TIMED_OUT,
        /// <summary>The snapshot could not be gathered or sent.</summary>
        FAILED
    }

    internal const string SYNC_FAILED_REASON = "The server could not send you the world. Its log has the details; try joining again.";

    /// <summary>
    ///     Sends the snapshot, or kicks the player with a reason if that fails.
    /// </summary>
    /// <remarks>
    ///     A failure used to be logged only: the queue went on to the next player while this one, already made a player
    ///     and getting every broadcast, waited on the loading screen with no end.
    /// </remarks>
    private async Task<bool> TrySendInitialSyncAsync(SessionId sessionId, string name)
    {
        try
        {
            await SendInitialSyncAsync(sessionId);
            return true;
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"Could not send the world to {name}; they are disconnected");
            // The kicked game disconnects itself (UserKickedProcessor), which cleans up the session
            await packetSender.SendPacketAsync(new PlayerKicked(SYNC_FAILED_REASON), sessionId);
            return false;
        }
    }

    /// <summary>
    ///     Waits until the player reports that it applied the snapshot, leaves, or takes longer than the timeout.
    /// </summary>
    internal static async Task<SyncOutcome> WaitForSyncAsync(Task finished, Func<bool> isConnected, TimeSpan timeout, TimeSpan pollPeriod, TimeProvider? timeProvider = null)
    {
        timeProvider ??= TimeProvider.System;
        long start = timeProvider.GetTimestamp();
        while (true)
        {
            if (finished.IsCompleted)
            {
                return SyncOutcome.FINISHED;
            }
            if (!isConnected())
            {
                return SyncOutcome.DISCONNECTED;
            }
            if (timeProvider.GetElapsedTime(start) >= timeout)
            {
                return SyncOutcome.TIMED_OUT;
            }
            // Never with a cancelled token: that used to throw out of the loop, so a player who timed out was never kicked
            await Task.WhenAny(finished, Task.Delay(pollPeriod, timeProvider));
        }
    }

    /// <summary>
    ///     A player reports that its game applied the snapshot.
    /// </summary>
    /// <returns>
    ///     True if the player is the one the queue waits for. A player whose sync had already timed out is announced to
    ///     the others here, as nothing else would announce it.
    /// </returns>
    public async Task<bool> SyncFinishedAsync(Player player)
    {
        lock (syncLocker)
        {
            if (syncingSession == player.SessionId && syncFinished != null)
            {
                syncFinished.TrySetResult();
                return true;
            }
        }
        logger.ZLogWarning($"{player.Name} finished loading after the join queue stopped waiting for them");
        await BroadcastPlayerJoinedAsync(player);
        return false;
    }

    private async Task JoinQueueLoop()
    {
        while (true)
        {
            lock (queueLocker)
            {
                if (joinQueue.Count == 0)
                {
                    queueActive = false;
                    return;
                }
            }

            try
            {
                SessionId sessionId = joinQueue.Dequeue();
                string? name = playerManager.GetPlayerReservation(sessionId)?.PlayerName;
                if (name == null)
                {
                    continue;
                }

                // Do this after dequeuing because everyone's position shifts forward
                SessionId[] array = [.. joinQueue];
                for (int i = 0; i < array.Length; i++)
                {
                    SessionId s = array[i];
                    await packetSender.SendPacketAsync(new JoinQueueInfo(i + 1, options.Value.InitialSyncTimeout), s);
                }

                logger.ZLogInformation($"Starting sync for player {name}");
                TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
                lock (syncLocker)
                {
                    syncingSession = sessionId;
                    syncFinished = finished;
                }
                SyncOutcome outcome;
                try
                {
                    if (await TrySendInitialSyncAsync(sessionId, name))
                    {
                        outcome = await WaitForSyncAsync(finished.Task, () => sessionManager.IsConnected(sessionId), TimeSpan.FromMilliseconds(options.Value.InitialSyncTimeout), SYNC_POLL_PERIOD);
                    }
                    else
                    {
                        outcome = SyncOutcome.FAILED;
                    }
                }
                finally
                {
                    lock (syncLocker)
                    {
                        syncingSession = null;
                        syncFinished = null;
                    }
                }

                switch (outcome)
                {
                    case SyncOutcome.FAILED:
                        // Told and kicked in TrySendInitialSyncAsync
                        break;
                    case SyncOutcome.DISCONNECTED:
                        logger.ZLogInformation($"Player {name} disconnected while syncing");
                        break;
                    case SyncOutcome.TIMED_OUT:
                        logger.ZLogInformation($"Initial sync timed out for player {name}");
                        // The kicked game disconnects itself (UserKickedProcessor)
                        await packetSender.SendPacketAsync(new PlayerKicked("Initial sync took too long and timed out"), sessionId);
                        break;
                    default:
                        logger.ZLogInformation($"Player {name} joined successfully. Remaining requests: {joinQueue.Count}");
                        if (!playerManager.TryGetPlayerBySessionId(sessionId, out Player? player))
                        {
                            throw new Exception($"Failed to get player object for session #{sessionId}");
                        }
                        await BroadcastPlayerJoinedAsync(player);
                        break;
                }
            }
            catch (Exception e)
            {
                logger.ZLogInformation($"Unexpected error during player connection inside the join queue: {e}");
            }
        }
    }

    public void AddToJoinQueue(SessionId sessionId)
    {
        // Necessary to avoid race conditions between JoinQueueLoop and AddToJoinQueue
        lock (queueLocker)
        {
            logger.ZLogInformation($"Added player {playerManager.GetPlayerReservation(sessionId)?.PlayerName} to queue");
            joinQueue.Enqueue(sessionId);

            if (queueActive)
            {
                packetSender.SendPacketAsync(new JoinQueueInfo(joinQueue.Count, options.Value.InitialSyncTimeout), sessionId);
            }
            else
            {
                // It may be possible to use the task's status itself for this,
                // but the ContinueWithHandleError callback might cause issues
                queueActive = true;
                Task.Run(JoinQueueLoop).ContinueWithHandleError();
            }
        }
    }

    private async Task SendInitialSyncAsync(SessionId sessionId)
    {
        Player player = playerManager.CreatePlayerData(sessionId, out bool wasBrandNewPlayer);
        (NitroxId assignedEscapePodId, EscapePodEntity? newlyCreatedEscapePod) = await escapePodManager.AssignPlayerToEscapePodAsync(player.PeerId);

        if (wasBrandNewPlayer)
        {
            player.SubRootId = assignedEscapePodId;
        }

        if (newlyCreatedEscapePod is { } validEscapePod)
        {
            SpawnEntities spawnNewEscapePod = new(validEscapePod);
            await packetSender.SendPacketToOthersAsync(spawnNewEscapePod, sessionId);
        }

        // TODO: Remove this code when security of player login is improved by https://github.com/SubnauticaNitrox/Nitrox/issues/1996
        // We need to reset permissions on join, otherwise players can impersonate an admin easily.
        player.Permissions = options.Value.DefaultPlayerPerm;

        // Make players on localhost admin by default.
        if (options.Value.LocalhostIsAdmin && sessionManager.GetEndPoint(sessionId)?.Address.IsLocalhost() == true)
        {
            logger.ZLogInformation($"Granted admin to '{player.Name}' because they're playing on the host machine");
            player.Permissions = Perms.ADMIN;
        }

        List<SimulatedEntity> simulations = entitySimulation.AssignGlobalRootEntitiesAndGetData(player);

        player.Entity = wasBrandNewPlayer ? SetupNewPlayerEntity(player) : RespawnExistingEntity(player);

        // With every Scanner Room, and the cameras in its docks, as a copy from one moment: the packet is serialized on
        // this thread while packet processors keep changing the live rooms on theirs.
        // And everything else copied whole, as players keep moving items between lockers while it is serialized
        List<GlobalRootEntity> globalRootEntities = EntityCopies.Copy(scannerRoomManager.DetachRooms(worldEntityManager.GetGlobalRootEntities(true)), out bool torn);
        if (torn)
        {
            logger.ZLogWarning($"The world kept changing while it was copied for {player.Name}; a few items may show only after a resync");
        }
        bool isFirstPlayer = playerManager.GetConnectedPlayers().Count == 1;

        // Beacons and vehicles that were removed since the player last joined; other players' pings are keyed by their entity
        PingPreferenceAuthority.RemoveGone(player.PingInstancePreferences,
                                           id => entityRegistry.TryGetEntityById(id, out Entity _) || playerManager.GetAllPlayers().Any(other => other.GameObjectId == id));

        InitialPlayerSync initialPlayerSync = new(
            player.GameObjectId,
            wasBrandNewPlayer,
            assignedEscapePodId,
            player.EquippedItems,
            player.UsedItems,
            player.QuickSlotsBindingIds,
            pdaManager.GetInitialPDAData(),
            storyManager.GetInitialStoryGoalData(storyScheduler, player),
            player.Position,
            player.Rotation,
            player.SubRootId,
            player.Stats,
            GetOtherPlayers(player),
            globalRootEntities,
            simulations,
            player.GameMode,
            player.Permissions,
            wasBrandNewPlayer ? IntroCinematicMode.LOADING : IntroCinematicMode.COMPLETED,
            new(new(player.PingInstancePreferences), player.PinnedRecipePreferences.ToList()),
            storyManager.GetTimeData(),
            isFirstPlayer,
            BuildingManager.GetEntitiesOperations(globalRootEntities),
            options.Value.KeepInventoryOnDeath,
            sessionSettings,
            player.InPrecursor,
            player.DisplaySurfaceWater,
            options.Value.MarkDeathPointsWithBeacon,
            activeTeleporters.GetAll(),
            player.UnreadPdaNotifications.ToList(),
            collectedTimeCapsules.GetAll(),
            player.DeathMarkers.ToList()
        );

        await packetSender.SendPacketAsync(initialPlayerSync, player.SessionId);

        IEnumerable<PlayerContext> GetOtherPlayers(Player player)
        {
            return playerManager.GetConnectedPlayers().Where(p => p != player).Select(p => p.PlayerContext);
        }

        PlayerEntity SetupNewPlayerEntity(Player player)
        {
            NitroxTransform transform = new(player.Position, player.Rotation, NitroxVector3.One);

            PlayerEntity playerEntity = new(transform, 0, null, false, player.GameObjectId, NitroxTechType.None, null, player.SubRootId.OrNull(), []);
            entityRegistry.AddOrUpdate(playerEntity);
            worldEntityManager.TrackEntityInTheWorld(playerEntity);
            return playerEntity;
        }

        PlayerEntity RespawnExistingEntity(Player player)
        {
            if (entityRegistry.TryGetEntityById(player.PlayerContext.PlayerNitroxId, out PlayerEntity playerWorldEntity))
            {
                return playerWorldEntity;
            }
            logger.ZLogError($"Unable to find player entity for {player.Name}. Re-creating one");
            return SetupNewPlayerEntity(player);
        }
    }

    private async Task BroadcastPlayerJoinedAsync(Player player)
    {
        PlayerJoinedMultiplayerSession playerJoinedPacket = new(player.PlayerContext, player.SubRootId, player.Entity);
        await packetSender.SendPacketToOthersAsync(playerJoinedPacket, player.SessionId);
    }

    public Task OnEventAsync(ISessionCleaner.Args args)
    {
        // They may have been queued, so just erase their entry
        joinQueue.RemoveWhere(sessionId => Equals(sessionId, args.Session.Id));
        return Task.CompletedTask;
    }
}
