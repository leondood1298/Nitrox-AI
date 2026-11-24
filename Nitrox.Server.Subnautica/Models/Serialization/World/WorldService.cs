using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Serialization;
using Nitrox.Model.Server;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities.Spawning;
using Nitrox.Server.Subnautica.Models.GameLogic.Players;
using Nitrox.Server.Subnautica.Models.GameLogic.Unlockables;
using Nitrox.Server.Subnautica.Services;
using Nitrox.Server.Subnautica.Services.Core;

namespace Nitrox.Server.Subnautica.Models.Serialization.World;

internal class WorldService : IHostedService
{
    private readonly EntityRegistry entityRegistry;
    private readonly EscapePodManager escapePodManager;
    private readonly ILogger<WorldService> logger;
    private readonly IOptions<SubnauticaServerOptions> options;
    private readonly PdaManager pdaManager;
    private readonly PlayerManager playerManager;

    private readonly TaskCompletionSource<bool> hasFinishedLoadingTcs = new();
    private readonly SaveService saveService;
    private readonly ScannerRoomDiagnostics scannerRoomDiagnostics;
    private readonly ScannerRoomManager scannerRoomManager;
    private readonly IProgressReporter progressReporter;
    private readonly IOptions<ServerStartOptions> startOptions;
    private readonly StoryManager storyManager;
    private readonly StoryScheduler storyScheduler;
    private readonly TimeService timeService;
    private readonly SaveGenerationStore generationStore = new();
    private readonly WorldEntityManager worldEntityManager;
    private readonly ActiveTeleporters activeTeleporters;
    private readonly CollectedTimeCapsules collectedTimeCapsules;
    public IServerSerializer Serializer { get; private set; }

    /// <summary>
    ///     False when the last save wrote exactly what was saved before, so there is nothing new to back up.
    /// </summary>
    public bool LastSaveChangedWorld { get; private set; } = true;
    private string FileEnding => Serializer?.FileEnding ?? "";

    public WorldService(
        ServerJsonSerializer jsonSerializer,
        IOptions<SubnauticaServerOptions> options,
        EntityRegistry entityRegistry,
        PlayerManager playerManager,
        StoryScheduler storyScheduler,
        StoryManager storyManager,
        WorldEntityManager worldEntityManager,
        TimeService timeService,
        PdaManager pdaManager,
        EscapePodManager escapePodManager,
        SaveService saveService,
        ScannerRoomDiagnostics scannerRoomDiagnostics,
        ScannerRoomManager scannerRoomManager,
        IProgressReporter progressReporter,
        IOptions<ServerStartOptions> startOptions,
        ActiveTeleporters activeTeleporters,
        CollectedTimeCapsules collectedTimeCapsules,
        ILogger<WorldService> logger)
    {
        this.options = options;
        this.entityRegistry = entityRegistry;
        this.playerManager = playerManager;
        this.storyScheduler = storyScheduler;
        this.storyManager = storyManager;
        this.worldEntityManager = worldEntityManager;
        this.timeService = timeService;
        this.pdaManager = pdaManager;
        this.escapePodManager = escapePodManager;
        this.saveService = saveService;
        this.scannerRoomDiagnostics = scannerRoomDiagnostics;
        this.scannerRoomManager = scannerRoomManager;
        this.progressReporter = progressReporter;
        this.startOptions = startOptions;
        this.activeTeleporters = activeTeleporters;
        this.collectedTimeCapsules = collectedTimeCapsules;
        this.logger = logger;

        Serializer = jsonSerializer;
    }

    public async Task<bool> SaveAsync(string saveDir, CancellationToken cancellationToken)
    {
        // Save data is collected from services so they must have finished loading at least once.
        try
        {
            await hasFinishedLoadingTcs.Task.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        // The save is written on this thread while packet processors keep changing the Scanner Rooms on theirs. It is
        // given a copy of every room, all taken at one moment, so the serializer needs to hold nothing still and
        // packet handling never waits for a save.
        List<GlobalRootEntity> globalRootEntities = scannerRoomManager.DetachRooms(worldEntityManager.GetPersistentGlobalRootEntities(),
            out IReadOnlyList<ScannerRoomSnapshot> savedRooms);
        (List<NitroxInt3> spawnedBatches, List<Entity> entities) = worldEntityManager.SnapshotForSave();
        PersistedWorldData persistedWorld = new()
        {
            WorldData = new()
            {
                ParsedBatchCells = spawnedBatches,
                GameData = GameData.From(pdaManager, storyManager.StoryGoalData, storyScheduler, storyManager, timeService, activeTeleporters, collectedTimeCapsules)
            },
            PlayerData = PlayerData.From(playerManager.GetAllPlayers()),
            GlobalRootData = GlobalRootData.From(globalRootEntities),
            EntityData = EntityData.From(PersistedEntities.SelectForSave(entities))
        };
        // Once files are being written the save must finish, so the stopping token is not passed on. Honoring it here
        // aborted a shutdown save after PlayerData/WorldData were written but before GlobalRootData/EntityData, leaving
        // a mixed-generation save.
        bool saved = Save(persistedWorld, saveDir);
        if (saved)
        {
            foreach (ScannerRoomSnapshot savedRoom in savedRooms)
            {
                scannerRoomDiagnostics.RecordCapturedCheckpoint("save", savedRoom.Room, ScannerRoomStateFingerprint.Create(savedRoom.Room),
                    ScannerRoomStateFingerprint.Validate(savedRoom.Room), "serialized_snapshot");
            }
        }
        return saved;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await progressReporter.ReportProgressAsync("Loading world data", 0.1f);
        
        if (!await LoadWorldFromSavePathAsync(startOptions.Value.GetServerSavePath()))
        {
            await CreateAndLoadWorldAsync();
        }
        
        await CreateFullEntityCacheIfRequested();
        return;

        async Task CreateFullEntityCacheIfRequested()
        {
            if (!options.Value.CreateFullEntityCache)
            {
                // No entity cache - server is ready after world data loads
                return;
            }

            try
            {
                logger.ZLogInformation($"Starting to load all batches up front.");
                logger.ZLogInformation($"This can take up to several minutes and you can't join until it's completed.");
                logger.ZLogInformation($"{entityRegistry.GetAllEntities().Count} entities already cached");
                if (entityRegistry.GetAllEntities().Count < 504732)
                {
                    await worldEntityManager.LoadAllUnspawnedEntitiesAsync(cancellationToken);
                }
                logger.ZLogInformation($"All batches have now been loaded.");
            }
            catch (OperationCanceledException ex)
            {
                logger.ZLogWarning($"Server start was cancelled by user:{Environment.NewLine}{ex.Message}");
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    internal void UpdateSerializer(IServerSerializer serverSerializer)
    {
        Validate.NotNull(serverSerializer, "Serializer cannot be null");
        Serializer = serverSerializer;
    }

    internal bool Save(PersistedWorldData persistedData, string saveDir)
    {
        try
        {
            SaveManifest manifest = generationStore.Write(saveDir,
            [
                new GenerationFile($"Version{FileEnding}", path => SerializeSnapshot(path, new SaveFileVersion())),
                new GenerationFile($"PlayerData{FileEnding}", path => SerializeSnapshot(path, persistedData.PlayerData)),
                new GenerationFile($"WorldData{FileEnding}", path => SerializeSnapshot(path, persistedData.WorldData)),
                new GenerationFile($"GlobalRootData{FileEnding}", path => SerializeSnapshot(path, persistedData.GlobalRootData)),
                new GenerationFile($"EntityData{FileEnding}", path => SerializeSnapshot(path, persistedData.EntityData))
            ], out bool changed);
            LastSaveChangedWorld = changed;

            logger.ZLogInformation($"World state saved (generation {manifest.Generation}{(changed ? "" : ", unchanged")})");
            return true;
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"Could not save world; the last complete save stays in place:");
            return false;
        }
    }

    /// <summary>
    ///     Writes one file of the save from the live game state.
    /// </summary>
    /// <remarks>
    ///     Packet processors keep changing entities on other threads while this runs, so a list can change under the
    ///     serializer and throw. The file is written into the unfinished generation only, so a throw cannot damage the
    ///     saved world; writing it again a moment later normally succeeds.
    /// </remarks>
    private void SerializeSnapshot(string path, object? data)
    {
        const int ATTEMPTS = 3;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                Serializer.Serialize(path, data);
                return;
            }
            catch (Exception ex) when (attempt < ATTEMPTS && IsConcurrentChange(ex))
            {
                logger.ZLogWarning($"{Path.GetFileName(path)} changed while being saved, writing it again (attempt {attempt + 1} of {ATTEMPTS})");
            }
        }

        static bool IsConcurrentChange(Exception? ex)
        {
            for (; ex != null; ex = ex.InnerException)
            {
                if (ex is InvalidOperationException)
                {
                    return true;
                }
            }
            return false;
        }
    }

    internal PersistedWorldData LoadPersistedWorld(ResolvedGeneration generation)
    {
        PersistedWorldData persistedData = new()
        {
            PlayerData = Serializer.Deserialize<PlayerData>(generation.PathOf($"PlayerData{FileEnding}")),
            WorldData = Serializer.Deserialize<WorldData>(generation.PathOf($"WorldData{FileEnding}")),
            GlobalRootData = Serializer.Deserialize<GlobalRootData>(generation.PathOf($"GlobalRootData{FileEnding}")),
            EntityData = Serializer.Deserialize<EntityData>(generation.PathOf($"EntityData{FileEnding}"))
        };
        if (!persistedData.IsValid())
        {
            throw new InvalidDataException("Save files are not valid");
        }
        EntityDataPostProcessing(persistedData.EntityData);
        GlobalRootDataPostProcessing(persistedData.GlobalRootData);
        IReadOnlyList<BuildEntity> removedEmptyBuildEntities = RemoveChildlessStructurallyEmptyBuildEntities(persistedData.GlobalRootData);
        foreach (BuildEntity buildEntity in removedEmptyBuildEntities)
        {
            logger.ZLogWarning($"Removed childless structurally empty base {buildEntity.Id} while loading the save.");
        }
        foreach (BuildEntity buildEntity in persistedData.GlobalRootData.Entities.OfType<BuildEntity>())
        {
            if (!buildEntity.TryIsStructurallyEmpty(out bool isStructurallyEmpty))
            {
                logger.ZLogError($"Base {buildEntity.Id} has malformed cell data and was retained for manual recovery.");
            }
            else if (isStructurallyEmpty)
            {
                logger.ZLogError($"Base {buildEntity.Id} is structurally empty but has {buildEntity.ChildEntities.Count} child entities, so the server retained and clients will quarantine it for manual recovery.");
            }
        }
        return persistedData;

        void EntityDataPostProcessing(EntityData entityData)
        {
            // Each entity once, children inside their parents again, whether the file was written this way or by an older
            // build which also listed every child at the top
            PersistedEntities.LoadedEntities loadedEntities = PersistedEntities.PrepareLoaded(entityData.Entities, persistedData.GlobalRootData.Entities);
            entityData.Entities = loadedEntities.Entities;
            if (loadedEntities.Orphans > 0)
            {
                logger.ZLogWarning($"Left out {loadedEntities.Orphans} saved entities whose parent was not saved (such as the contents of a destroyed Cyclops).");
            }
            if (loadedEntities.DuplicateChildren > 0)
            {
                logger.ZLogInformation($"Removed {loadedEntities.DuplicateChildren} stale copies of children that had the id of another child under the same parent (such as a dropped tool's battery).");
            }
            if (loadedEntities.SerializedDuplicates > 0)
            {
                logger.ZLogInformation($"Removed {loadedEntities.SerializedDuplicates} copies of the game's lights and other fixed objects, which an older build made twice.");
            }

            bool brokenEntitiesFound = false;

            foreach (Entity entity in entityData.Entities)
            {
                if (entity is WorldEntity we)
                {
                    NitroxVector3 pos = we.Transform.LocalPosition;
                    if (float.IsNaN(pos.X) || float.IsNaN(pos.Y) || float.IsNaN(pos.Z) ||
                        float.IsInfinity(pos.X) || float.IsInfinity(pos.Y) || float.IsInfinity(pos.Z))
                    {
                        logger.ZLogError($"Found {nameof(WorldEntity)} with NaN or infinite position. Teleporting it to world origin.");
                        we.Transform.LocalPosition = NitroxVector3.Zero;
                    }

                    NitroxQuaternion rot = we.Transform.LocalRotation;
                    if (float.IsNaN(rot.X) || float.IsNaN(rot.Y) || float.IsNaN(rot.Z) || float.IsNaN(rot.W) ||
                        float.IsInfinity(rot.X) || float.IsInfinity(rot.Y) || float.IsInfinity(rot.Z) || float.IsInfinity(rot.W))
                    {
                        logger.ZLogError($"Found {nameof(WorldEntity)} with NaN or infinite rotation. Resetting rotation.");
                        we.Transform.LocalRotation = NitroxQuaternion.Identity;
                    }

                    if (we.Level == 100)
                    {
                        logger.ZLogError($"Found a world entity with cell level 100. It will be hotfixed.\n{we}");
                        we.Level = 0;
                        brokenEntitiesFound = true;
                    }
                }
            }

            if (brokenEntitiesFound)
            {
                logger.ZLogWarningOnce($"Please consider reporting the above issues on the Discord server.");
            }
        }

        void GlobalRootDataPostProcessing(GlobalRootData globalRootData)
        {
            foreach (GlobalRootEntity entity in globalRootData.Entities)
            {
                EnsureChildrenTransformAreParented(entity);
            }

            void EnsureChildrenTransformAreParented(WorldEntity entity)
            {
                if (entity.Transform == null)
                {
                    return;
                }

                bool brokenEntitiesFound = false;

                foreach (Entity child in entity.ChildEntities)
                {
                    if (child is not WorldEntity childWe)
                    {
                        continue;
                    }

                    if (child is not GlobalRootEntity && childWe.Level == 100)
                    {
                        logger.ZLogError($"Found a world entity with cell level 100. It will be hotfixed.\n{childWe}");
                        childWe.Level = 0;
                        brokenEntitiesFound = true;
                    }

                    if (childWe.Transform != null)
                    {
                        childWe.Transform.SetParent(entity.Transform, false);
                        EnsureChildrenTransformAreParented(childWe);
                    }
                }

                if (brokenEntitiesFound)
                {
                    logger.ZLogWarningOnce($"Please consider reporting the above issues on the Discord server.");
                }
            }
        }
    }

    internal static IReadOnlyList<BuildEntity> RemoveChildlessStructurallyEmptyBuildEntities(GlobalRootData globalRootData)
    {
        List<BuildEntity> removedBuildEntities = [];
        foreach (BuildEntity buildEntity in globalRootData.Entities.OfType<BuildEntity>().ToList())
        {
            if (!buildEntity.TryIsStructurallyEmpty(out bool isStructurallyEmpty) ||
                !isStructurallyEmpty ||
                buildEntity.ChildEntities.Count != 0)
            {
                continue;
            }

            globalRootData.Entities.Remove(buildEntity);
            removedBuildEntities.Add(buildEntity);
        }
        return removedBuildEntities;
    }

    // TODO: This method should be removed. Each service should load its own data instead of centralizing it here.
    private async Task LoadPersistedWorldIntoServicesAsync(PersistedWorldData pWorldData)
    {
        try
        {
            string seed = options.Value.Seed;
            logger.ZLogInformation($"Loading world with seed {seed}");

            // Time
            timeService.ActiveTime = TimeSpan.FromSeconds(pWorldData.WorldData.GameData.StoryTiming.RealTimeElapsed);
            timeService.GameTime = TimeSpan.FromSeconds(pWorldData.WorldData.GameData.StoryTiming.ElapsedSeconds);
            // Entities
            entityRegistry.AddEntities(pWorldData.EntityData.Entities);
            entityRegistry.AddEntitiesIgnoringDuplicate(pWorldData.GlobalRootData.Entities.OfType<Entity>().ToList());
            int repairedCrashHomes = CrashHomeRespawns.RepairAfterLoad(entityRegistry.GetAllEntities());
            if (repairedCrashHomes > 0)
            {
                logger.ZLogInformation($"{repairedCrashHomes} crash homes without their crashfish make a new one when next loaded.");
            }
            // Before loaded entities become available to automatic simulation.
            scannerRoomManager.WorldLoaded();
            LavaLarvaAttachments.DetachFromMissingVehicles(entityRegistry);
            await escapePodManager.AddKnownPodsAsync(entityRegistry.GetEntities<EscapePodEntity>());

            // TODO: hacky code - see WorldEntityManager for more information.
            List<WorldEntity> worldEntities = entityRegistry.GetEntities<WorldEntity>();
            worldEntityManager.globalRootEntitiesById = entityRegistry.GetEntities<GlobalRootEntity>().ToDictionary(entity => entity.Id);
            worldEntityManager.worldEntitiesByCell = worldEntities.Where(entity => entity is not GlobalRootEntity && entity.ParentId == null)
                                                                  .GroupBy(entity => entity.AbsoluteEntityCell)
                                                                  .ToDictionary(group => group.Key, group => group.ToDictionary(entity => entity.Id, entity => entity));

            foreach (Player player in pWorldData.PlayerData.GetPlayers())
            {
                playerManager.AddSavedPlayer(player);
            }
            worldEntityManager.LoadSpawnedBatches(pWorldData.WorldData.ParsedBatchCells ?? []);
            // Pda
            pdaManager.PdaState = pWorldData.WorldData.GameData.PDAState;
            // Story progression
            storyManager.StoryGoalData = pWorldData.WorldData.GameData.StoryGoals;
            await storyScheduler.ScheduleStoriesIfNotInPast(pWorldData.WorldData.GameData.StoryGoals.ScheduledGoals);
            // Global story timed events
            storyManager.AuroraCountdownTimeMs = pWorldData.WorldData.GameData.StoryTiming.AuroraCountdownTime ?? storyManager.GenerateDeterministicAuroraTime(seed);
            storyManager.AuroraWarningTimeMs = pWorldData.WorldData.GameData.StoryTiming.AuroraWarningTime ?? timeService.GameTime.TotalMilliseconds;
            // +27 is from CrashedShipExploder.IsExploded, -480 is from the default time (see TimeService)
            storyManager.AuroraRealExplosionTime = TimeSpan.FromSeconds(pWorldData.WorldData.GameData.StoryTiming.AuroraRealExplosionTime ?? storyManager.AuroraCountdownTimeMs * 0.001 + 27 - TimeService.DEFAULT_STARTING_GAME_TIME_SECONDS);
            // Precursor teleporters
            activeTeleporters.Load(pWorldData.WorldData.GameData.ActiveTeleporters);
            // Encyclopedia entries of the collected time capsules
            collectedTimeCapsules.Load(pWorldData.WorldData.GameData.CollectedTimeCapsules);
        }
        finally
        {
            hasFinishedLoadingTcs.TrySetResult(true);
        }

        logger.ZLogInformation($"World finished loading");
    }

    /// <summary>
    ///     Loads the newest whole save of the world: the last save, else the one before it, else the newest backup.
    /// </summary>
    /// <returns>False if the world has never been saved, so a new one is to be made.</returns>
    /// <exception cref="WorldLoadException">The world is of another save format, or none of its saves can be loaded.</exception>
    private async Task<bool> LoadWorldFromSavePathAsync(string saveDir)
    {
        string worldName = Path.GetFileName(saveDir);
        string versionFileName = $"Version{FileEnding}";
        IReadOnlyList<ResolvedGeneration> generations = Directory.Exists(saveDir) ? generationStore.FindGenerations(saveDir) : [];

        // The newest files decide whether this build can read the world at all; falling back to an older save of a world
        // from another build would only find that build's format again.
        string newestVersionPath = generations.Count > 0 ? generations[0].PathOf(versionFileName) : Path.Combine(saveDir, versionFileName);
        if (generations.Count == 0 && (!File.Exists(newestVersionPath) || new FileInfo(newestVersionPath).Length == 0))
        {
            // The launcher makes a new world with an empty Version file
            logger.ZLogWarning($"No previous save file found, creating a new one");
            return false;
        }
        if (TryReadSaveFormat(newestVersionPath) is { } newestFormat && NitroxSaveFormat.DescribeMismatch(worldName, newestFormat) is { } mismatch)
        {
            throw new WorldLoadException(mismatch);
        }

        HashSet<string> triedBackups = new(StringComparer.OrdinalIgnoreCase);
        string backupsDir = startOptions.Value.GetServerSaveBackupsPath();
        PersistedWorldData? loaded = null;
        foreach (ResolvedGeneration candidate in Candidates())
        {
            try
            {
                if (TryReadSaveFormat(candidate.PathOf(versionFileName)) is not { } candidateFormat || !NitroxSaveFormat.CanRead(candidateFormat))
                {
                    logger.ZLogWarning($"Skipped the {candidate.Description} of world \"{worldName}\": it is of another save format.");
                    continue;
                }
                loaded = LoadPersistedWorld(candidate);
                ReportFallback(candidate);
                break;
            }
            catch (Exception ex)
            {
                logger.ZLogError(ex, $"Could not load the {candidate.Description} of world \"{worldName}\" (generation {candidate.Manifest.Generation}):");
            }
            finally
            {
                if (candidate.Source == GenerationSource.BACKUP)
                {
                    SaveGenerationStore.RemoveRestoreFolder(saveDir);
                }
            }
        }
        if (loaded == null)
        {
            throw new WorldLoadException($"No save of world \"{worldName}\" could be loaded: not the last save, not the one before it and no backup. The files were left as they are; the log above says what was wrong with each.");
        }

        await LoadPersistedWorldIntoServicesAsync(loaded);
        return true;

        IEnumerable<ResolvedGeneration> Candidates()
        {
            foreach (ResolvedGeneration generation in generations)
            {
                yield return generation;
            }
            while (generationStore.ExtractNewestBackup(saveDir, backupsDir, triedBackups) is { } backup)
            {
                yield return backup;
            }
        }

        void ReportFallback(ResolvedGeneration used)
        {
            switch (used.Source)
            {
                case GenerationSource.UNFINISHED_SAVE:
                    logger.ZLogWarning($"The last save of world \"{worldName}\" stopped while its files were being put in place. All of them had been written, so that save (generation {used.Manifest.Generation}) was loaded and nothing is lost.");
                    break;
                case GenerationSource.PREVIOUS:
                    logger.ZLogWarning($"The last save of world \"{worldName}\" is incomplete or damaged. Loaded the save before it (generation {used.Manifest.Generation}) instead; what happened after that save is lost.");
                    break;
                case GenerationSource.BACKUP:
                    logger.ZLogWarning($"Neither the last save of world \"{worldName}\" nor the one before it could be loaded. Loaded the {used.Description} (generation {used.Manifest.Generation}) instead; what happened after that backup is lost.");
                    break;
            }
        }
    }

    private async Task CreateAndLoadWorldAsync()
    {
        PersistedWorldData pWorldData = new()
        {
            EntityData = EntityData.From([]),
            PlayerData = PlayerData.From([]),
            WorldData = new WorldData
            {
                GameData = new GameData
                {
                    PDAState = new PdaStateData(),
                    StoryGoals = new StoryGoalData(),
                    StoryTiming = new StoryTimingData()
                },
                ParsedBatchCells = []
            },
            GlobalRootData = new GlobalRootData()
        };
        await LoadPersistedWorldIntoServicesAsync(pWorldData);
        InitNewWorld();

        void InitNewWorld()
        {
            // Creating entities for the 11 RadiationLeakPoint located at (Aurora Scene) //Aurora-MainPrefab/Aurora/radiationleaks/RadiationLeaks(Clone)
            const int TOTAL_LEAKS = 11; // This constant is defined by Subnautica and should never be modified
            for (int i = 0; i < TOTAL_LEAKS; i++)
            {
                RadiationLeakEntity leakEntity = new(new(), i, new(0));
                worldEntityManager.AddOrUpdateGlobalRootEntity(leakEntity);
            }
        }
    }

    /// <summary>
    ///     Reads the <see cref="NitroxSaveFormat" /> number from a Version file, or null when the file cannot be read.
    /// </summary>
    /// <remarks>
    ///     Read by hand because <see cref="SaveFileVersion" /> has get-only properties, which the server's serializer
    ///     leaves at this build's values instead of filling from the file.
    /// </remarks>
    internal static int? TryReadSaveFormat(string versionPath)
    {
        try
        {
            JObject version = JObject.Parse(File.ReadAllText(versionPath));
            return version.Value<int?>(nameof(SaveFileVersion.SaveFormat)) ?? NitroxSaveFormat.UNKNOWN;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
