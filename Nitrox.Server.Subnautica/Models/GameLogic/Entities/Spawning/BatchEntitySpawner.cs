using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Factories;
using Nitrox.Server.Subnautica.Models.Helper;
using Nitrox.Server.Subnautica.Models.Resources;
using Nitrox.Server.Subnautica.Models.Resources.Core;
using Nitrox.Server.Subnautica.Models.Resources.Parsers;
using Nitrox.Server.Subnautica.Models.Serialization;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities.Spawning;

/// <summary>
///     Fills one batch of the world from the game's baked cell files. Everything random comes from the batch's own random
///     (<see cref="RandomFactory.GetBatchRandom" />), so the same seed fills a batch the same way in any order.
/// </summary>
internal sealed class BatchEntitySpawner(
    RandomFactory randomFactory,
    SubnauticaUweWorldEntityFactory worldEntityFactory,
    IUwePrefabFactory prefabFactory,
    IEntityBootstrapperManager entityBootstrapperManager,
    PdaManager pdaManager,
    PrefabPlaceholderGroupsResource prefabPlaceholderGroupsResource,
    BatchCellsParser batchCellsParser,
    IOptions<SubnauticaServerOptions> options,
    ILogger<BatchEntitySpawner> logger) : IBatchEntityGenerator
{
    /// <summary>
    ///     Radius of the sphere each further copy of a slot's prefab is moved within, from the previous copy (EntitySlot.SpawnVirtualEntities).
    /// </summary>
    private const float SLOT_COPY_OFFSET_RADIUS = 4f;

    private static readonly NitroxQuaternion prefabZUpRotation = NitroxQuaternion.FromEuler(new(-90f, 0f, 0f));

    private readonly BatchCellsParser batchCellsParser = batchCellsParser;
    private readonly IEntityBootstrapperManager entityBootstrapperManager = entityBootstrapperManager;
    private readonly ILogger<BatchEntitySpawner> logger = logger;
    private readonly IOptions<SubnauticaServerOptions> options = options;
    private readonly PdaManager pdaManager = pdaManager;
    private readonly IUwePrefabFactory prefabFactory = prefabFactory;
    private readonly PrefabPlaceholderGroupsResource prefabPlaceholderGroupsResource = prefabPlaceholderGroupsResource;
    private readonly RandomFactory randomFactory = randomFactory;
    private readonly SubnauticaUweWorldEntityFactory worldEntityFactory = worldEntityFactory;

    public async Task<List<Entity>> GenerateAsync(NitroxInt3 batchId, bool fullCacheCreation)
    {
        Batch batch = new(new DeterministicGenerator(options.Value.Seed, batchId.ToString()), randomFactory.GetBatchRandom(batchId));
        List<EntitySpawnPoint> spawnPoints = batchCellsParser.ParseBatchData(batchId);
        List<Entity> entities = await SpawnEntitiesAsync(spawnPoints, batch);

        if (entities.Count > 0 && !fullCacheCreation)
        {
            logger.ZLogInformation($"Spawning {entities.Count} entities from {spawnPoints.Count} spawn points in batch {batchId}");
        }

        HashSet<Entity> seen = [];
        foreach (Entity entity in entities)
        {
            if (!seen.Add(entity))
            {
                logger.ZLogError($"Duplicate Entity detected! {entity}");
            }
        }

        return entities;
    }

    /// <inheritdoc cref="CreateEntityWithChildrenAsync" />
    private async Task<List<Entity>> SpawnEntitiesUsingRandomDistributionAsync(EntitySpawnPoint entitySpawnPoint, List<UwePrefab> prefabs, Batch batch, Entity? parentEntity = null)
    {
        // See CSVEntitySpawner.GetPrefabForSlot for reference
        (List<UwePrefab>? allowedPrefabs, float fragmentProbability, float completeFragmentProbability) = await FilterAllowedPrefabsAsync(prefabs, entitySpawnPoint);

        bool areFragmentProbabilitiesNonNull = fragmentProbability > 0f && completeFragmentProbability > 0f;
        float probabilityMultiplier = areFragmentProbabilitiesNonNull ? (completeFragmentProbability + fragmentProbability) / fragmentProbability : 1f;
        float weightedFragmentProbability = 0f;
        for (int i = 0; i < allowedPrefabs.Count; i++)
        {
            UwePrefab prefab = allowedPrefabs[i];
            if (areFragmentProbabilitiesNonNull && prefab.IsFragment)
            {
                prefab = prefab with { Probability = prefab.Probability * probabilityMultiplier };
                allowedPrefabs[i] = prefab;
            }
            weightedFragmentProbability += prefab.Probability;
        }

        UwePrefab chosenPrefab = default;
        if (weightedFragmentProbability > 0f)
        {
            float probabilityThreshold = batch.Random.NextFloat();
            if (weightedFragmentProbability > 1f)
            {
                probabilityThreshold *= weightedFragmentProbability;
            }
            float currentProbability = 0f;
            foreach (UwePrefab prefab in allowedPrefabs)
            {
                currentProbability += prefab.Probability;
                if (currentProbability >= probabilityThreshold)
                {
                    chosenPrefab = prefab;
                    break;
                }
            }
        }

        if (chosenPrefab.Count == 0)
        {
            return [];
        }

        List<Entity> spawnedEntities = [];
        if (await worldEntityFactory.FindAsync(chosenPrefab.ClassId) is { } uweWorldEntity)
        {
            NitroxVector3 offset = NitroxVector3.Zero;
            for (int i = 0; i < chosenPrefab.Count; i++)
            {
                // As in EntitySlot.SpawnVirtualEntities: each further copy is moved from where the previous one went
                if (i > 0)
                {
                    offset += batch.Random.NextInsideSphere(SLOT_COPY_OFFSET_RADIUS);
                }
                List<Entity> entities = [.. await CreateEntityWithChildrenAsync(entitySpawnPoint,
                                                                                chosenPrefab.ClassId,
                                                                                uweWorldEntity.TechType,
                                                                                uweWorldEntity.PrefabZUp,
                                                                                uweWorldEntity.CellLevel,
                                                                                uweWorldEntity.LocalScale,
                                                                                batch,
                                                                                parentEntity,
                                                                                offset)];

                // Unparenting the new entity because it's directly under the cell root
                entities[0].ParentId = null;
                if (entities[0] is WorldEntity worldEntity)
                {
                    worldEntity.Transform.SetParent(null, true);
                }

                spawnedEntities.AddRange(entities);
            }
        }

        return spawnedEntities;
    }

    private async Task<(List<UwePrefab> allowedPrefabs, float fragmentProbability, float completeFragmentProbability)> FilterAllowedPrefabsAsync(List<UwePrefab> prefabs, EntitySpawnPoint entitySpawnPoint)
    {
        List<UwePrefab> allowedPrefabs = [];

        float fragmentProbability = 0;
        float completeFragmentProbability = 0;
        for (int i = 0; i < prefabs.Count; i++)
        {
            UwePrefab prefab = prefabs[i];
            // Adapted code from the while loop in CSVEntitySpawner.GetPrefabForSlot
            if (prefab.ClassId != "None" && await worldEntityFactory.FindAsync(prefab.ClassId) is {} uweWorldEntity &&
                entitySpawnPoint.AllowedTypes.Contains(uweWorldEntity.SlotType))
            {
                float weightedProbability = prefab.Probability / entitySpawnPoint.Density;
                if (weightedProbability > 0)
                {
                    if (prefab.IsFragment)
                    {
                        if (pdaManager.ContainsCompletelyScannedTech(uweWorldEntity.TechType))
                        {
                            completeFragmentProbability += weightedProbability;
                            continue;
                        }
                        else
                        {
                            fragmentProbability += weightedProbability;
                        }
                    }
                    prefab = prefab with { Probability = weightedProbability };
                    allowedPrefabs.Add(prefab);
                }
            }
        }

        return (allowedPrefabs, fragmentProbability, completeFragmentProbability);
    }

    /// <summary>
    /// Spawns the regular (can be children of PrefabPlaceholdersGroup) which are always the same thus context independent.
    /// </summary>
    /// <inheritdoc cref="CreateEntityWithChildrenAsync" />
    private async Task<List<Entity>> SpawnEntitiesStaticallyAsync(EntitySpawnPoint entitySpawnPoint, Batch batch, WorldEntity? parentEntity = null)
    {
        if (await worldEntityFactory.FindAsync(entitySpawnPoint.ClassId) is { } uweWorldEntity)
        {
            // prefabZUp should not be taken into account for statically spawned entities
            return await CreateEntityWithChildrenAsync(entitySpawnPoint,
                                                       entitySpawnPoint.ClassId,
                                                       uweWorldEntity.TechType,
                                                       false,
                                                       uweWorldEntity.CellLevel,
                                                       entitySpawnPoint.Scale,
                                                       batch,
                                                       parentEntity);
        }

        return [];
    }

    /// <returns>The first entity is a <see cref="WorldEntity"/> and the following are its children</returns>
    private async Task<List<Entity>> CreateEntityWithChildrenAsync(EntitySpawnPoint entitySpawnPoint, string classId, NitroxTechType techType, bool prefabZUp, int cellLevel, NitroxVector3 localScale, Batch batch, Entity? parentEntity = null, NitroxVector3 positionOffset = default)
    {
        WorldEntity spawnedEntity;
        NitroxVector3 position = entitySpawnPoint.LocalPosition + positionOffset;
        NitroxQuaternion rotation = entitySpawnPoint.LocalRotation;
        if (prefabZUp)
        {
            // See EntitySlot.SpawnVirtualEntities use of WorldEntityInfo.prefabZUp
            rotation *= prefabZUpRotation;
        }

        if (classId == CellRootEntity.CLASS_ID)
        {
            spawnedEntity = new CellRootEntity(position,
                                               rotation,
                                               localScale,
                                               techType,
                                               cellLevel,
                                               classId,
                                               true,
                                               batch.Ids.NextId());
        }
        else
        {
            prefabPlaceholderGroupsResource.PickRandomClassIdIfRequired(ref classId, batch.Random);
            spawnedEntity = new WorldEntity(position,
                                            rotation,
                                            localScale,
                                            techType,
                                            cellLevel,
                                            classId,
                                            true,
                                            batch.Ids.NextId(),
                                            parentEntity);
        }

        // See EntitySlotsPlaceholder.Spawn
        (bool createResult, spawnedEntity) = await TryCreatePrefabPlaceholdersGroupWithChildrenAsync(spawnedEntity, classId, batch);
        if (!createResult)
        {
            spawnedEntity.ChildEntities = await SpawnEntitiesAsync(entitySpawnPoint.Children, batch, spawnedEntity);
        }

        spawnedEntity = await entityBootstrapperManager.PrepareEntityIfRequired(spawnedEntity, batch.Ids, batch.Random);

        // Order of addition is important, first, the created entity, then its children and the cell root children it created
        // so that this output can always be used to get the created entity
        List<Entity> spawnedEntities = [spawnedEntity];

        for (int i = spawnedEntity.ChildEntities.Count - 1; i >= 0; i--)
        {
            Entity spawnedChildEntity = spawnedEntity.ChildEntities[i];
            // Regular case in which a spawned child would actually need to be spawned under the cell root instead of the current entity
            if (spawnedChildEntity.ParentId != spawnedEntity.Id)
            {
                spawnedEntity.ChildEntities.RemoveAt(i);
                spawnedEntities.Add(spawnedChildEntity);
            }
        }

        // Ensures children are only returned at the top level
        if (parentEntity == null)
        {
            // Children are added as well so they can be indexed at the top level
            // (for use by simulation ownership and various other consumers).
            spawnedEntities.AddRange(AllChildren(spawnedEntity));
        }

        return spawnedEntities;
    }

    private static List<Entity> AllChildren(Entity entity)
    {
        List<Entity> allChildren = [.. entity.ChildEntities];

        for (int i = 0; i < allChildren.Count; i++)
        {
            allChildren.AddRange(allChildren[i].ChildEntities);
        }

        return allChildren;
    }

    private async Task<List<Entity>> SpawnEntitiesAsync(List<EntitySpawnPoint> entitySpawnPoints, Batch batch, WorldEntity? parentEntity = null)
    {
        List<Entity> entities = [];
        foreach (EntitySpawnPoint esp in entitySpawnPoints)
        {
            if (esp is SerializedEntitySpawnPoint serializedEsp)
            {
                // We add the cell's coordinate because this entity isn't parented so it needs to know about its global position
                NitroxTransform transform = new(serializedEsp.LocalPosition + serializedEsp.AbsoluteEntityCell.Position, serializedEsp.LocalRotation, serializedEsp.Scale);
                SerializedWorldEntity entity = new(serializedEsp.SerializedComponents, serializedEsp.Layer, transform, batch.Ids.NextId(), parentEntity?.Id, serializedEsp.AbsoluteEntityCell);
                entities.Add(entity);
                continue;
            }

            if (esp.Density > 0)
            {
                if (await prefabFactory.TryGetPossiblePrefabsAsync(esp.BiomeType) is [_, ..] prefabs)
                {
                    entities.AddRange(await SpawnEntitiesUsingRandomDistributionAsync(esp, prefabs, batch, parentEntity));
                }
                else if (!string.IsNullOrEmpty(esp.ClassId))
                {
                    entities.AddRange(await SpawnEntitiesStaticallyAsync(esp, batch, parentEntity));
                }
            }
        }

        return entities;
    }

    /// <summary>
    /// Check to see if this entity is a PrefabPlaceholderGroup.
    /// If it is, we want to add the PrefabPlaceholders that would be spawned here.
    /// This is suppressed on the client so we don't get virtual entities that the server doesn't know about.
    /// </summary>
    /// <returns>If this Entity is a PrefabPlaceholdersGroup</returns>
    private async Task<(bool Success, WorldEntity ChangedEntity)> TryCreatePrefabPlaceholdersGroupWithChildrenAsync(WorldEntity entity, string classId, Batch batch)
    {
        if (!prefabPlaceholderGroupsResource.GroupsByClassId.TryGetValue(classId, out PrefabPlaceholdersGroupAsset groupAsset))
        {
            return (false, entity);
        }

        entity = new PlaceholderGroupWorldEntity(entity);

        // Adapted from PrefabPlaceholdersGroup.Spawn
        for (int i = 0; i < groupAsset.PrefabAssets.Length; i++)
        {
            // Fix positioning of children
            IPrefabAsset prefabAsset = groupAsset.PrefabAssets[i];

            // Two cases, either the PrefabPlaceholder holds a visible GameObject or an EntitySlot (a MB which has a chance of spawning a prefab)
            if (prefabAsset is PrefabPlaceholderAsset placeholderAsset && placeholderAsset.EntitySlot.HasValue)
            {
                WorldEntity? spawnedEntity = await SpawnPrefabAssetInEntitySlotAsync(placeholderAsset.Transform, placeholderAsset.EntitySlot.Value, batch, entity.AbsoluteEntityCell, entity);
                if (spawnedEntity != null)
                {
                    // Spawned child will not be of the same type as the current prefabAsset
                    if (prefabPlaceholderGroupsResource.GroupsByClassId.ContainsKey(spawnedEntity.ClassId))
                    {
                        spawnedEntity = new PlaceholderGroupWorldEntity(spawnedEntity, i);
                    }
                    entity.ChildEntities.Add(spawnedEntity);
                }
            }
            else
            {
                // Regular visible GameObject
                string prefabClassId = prefabAsset.ClassId;
                if (prefabAsset is PrefabPlaceholderRandomAsset randomAsset && randomAsset.ClassIds.Count > 0)
                {
                    int randomIndex = batch.Random.NextIntRange(0, randomAsset.ClassIds.Count);
                    prefabClassId = randomAsset.ClassIds[randomIndex];
                }

                EntitySpawnPoint esp = new(entity.AbsoluteEntityCell, prefabAsset.Transform.Position, prefabAsset.Transform.Rotation, prefabAsset.Transform.LocalScale, prefabClassId);
                WorldEntity spawnedEntity = (WorldEntity)(await SpawnEntitiesStaticallyAsync(esp, batch, entity)).First();
                if (prefabAsset is PrefabPlaceholdersGroupAsset)
                {
                    spawnedEntity = new PlaceholderGroupWorldEntity(spawnedEntity, i);
                }
                else
                {
                    spawnedEntity = new PrefabPlaceholderEntity(spawnedEntity, i);
                }

                entity.ChildEntities.Add(spawnedEntity);
            }
        }

        return (true, entity);
    }

    private async Task<WorldEntity?> SpawnPrefabAssetInEntitySlotAsync(NitroxTransform transform, NitroxEntitySlot entitySlot, Batch batch, AbsoluteEntityCell cell, Entity parentEntity)
    {
        if (await prefabFactory.TryGetPossiblePrefabsAsync(entitySlot.BiomeType) is not [_, ..] prefabs)
        {
            return null;
        }
        List<Entity> entities = [];

        EntitySpawnPoint entitySpawnPoint = new(cell, transform.Position, transform.Rotation, [.. entitySlot.AllowedTypes], entitySlot.Density, entitySlot.BiomeType);
        entities.AddRange(await SpawnEntitiesUsingRandomDistributionAsync(entitySpawnPoint, prefabs, batch, parentEntity));
        if (entities.Count > 0)
        {
            return (WorldEntity)entities[0];
        }
        return null;
    }

    /// <param name="Ids">Gives the batch's entity ids.</param>
    /// <param name="Random">Makes every random choice of the batch.</param>
    private sealed record Batch(DeterministicGenerator Ids, XorRandom Random);
}
