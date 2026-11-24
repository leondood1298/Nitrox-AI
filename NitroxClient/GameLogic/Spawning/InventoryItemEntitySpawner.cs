using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Nitrox.Model.DataStructures;
using NitroxClient.Communication;
using NitroxClient.GameLogic.Helper;
using NitroxClient.GameLogic.Spawning.Abstract;
using NitroxClient.GameLogic.Spawning.Metadata;
using NitroxClient.GameLogic.Spawning.WorldEntities;
using NitroxClient.MonoBehaviours;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;
using UWE;

namespace NitroxClient.GameLogic.Spawning;

public class InventoryItemEntitySpawner(EntityMetadataManager entityMetadataManager) : SyncEntitySpawner<InventoryItemEntity>
{
    /// <summary>
    ///     How long a seed of another player waits for this game's own seed to leave the slot they were both planted in.
    /// </summary>
    private const float SLOT_WAIT_SECONDS = 10f;

    private readonly EntityMetadataManager entityMetadataManager = entityMetadataManager;

    protected override IEnumerator SpawnAsync(InventoryItemEntity entity, TaskResult<Optional<GameObject>> result)
    {        
        if (!CanSpawn(entity, out GameObject? parentObject, out ItemsContainer? container, out string? errorLog))
        {
            Log.Error(errorLog);
            result.Set(Optional.Empty);
            yield break;
        }

        TaskResult<GameObject> gameObjectResult = new();
        yield return DefaultWorldEntitySpawner.CreateGameObject(entity.TechType.ToUnity(), entity.ClassId, entity.Id, gameObjectResult);
        GameObject gameObject = gameObjectResult.Get();

        SetupObject(entity, gameObject, parentObject, container);

        result.Set(Optional.Of(gameObject));
    }

    protected override bool SpawnSync(InventoryItemEntity entity, TaskResult<Optional<GameObject>> result)
    {
        if (!DefaultWorldEntitySpawner.TryGetCachedPrefab(out GameObject prefab, entity.TechType.ToUnity(), entity.ClassId))
        {
            return false;
        }
        if (!CanSpawn(entity, out GameObject? parentObject, out ItemsContainer? container, out string? errorLog))
        {
            Log.Error(errorLog);
            return true;
        }

        GameObject gameObject = GameObjectExtensions.SpawnFromPrefab(prefab, entity.Id);

        SetupObject(entity, gameObject, parentObject, container);

        result.Set(gameObject);
        return true;
    }

    protected override bool SpawnsOwnChildren(InventoryItemEntity entity) => false;

    private bool CanSpawn(InventoryItemEntity entity, [NotNullWhen(true)] out GameObject? parentObject, [NotNullWhen(true)] out ItemsContainer? container, [NotNullWhen(false)] out string? errorLog)
    {
        Optional<GameObject> owner = NitroxEntity.GetObjectFrom(entity.ParentId);
        if (!owner.HasValue)
        {
            parentObject = null;
            container = null;
            errorLog = $"Unable to find inventory container with id {entity.Id} for {entity}";
            return false;
        }

        Optional<ItemsContainer> opContainer = InventoryContainerHelper.TryGetContainerByOwner(owner.Value);

        if (!opContainer.HasValue)
        {
            parentObject = null;
            container = null;
            errorLog = $"Could not find container field on GameObject {parentObject.AliveOrNull()?.GetFullHierarchyPath()}";
            return false;
        }

        parentObject = owner.Value;
        container = opContainer.Value;
        errorLog = null;
        return true;
    }

    private void SetupObject(InventoryItemEntity entity, GameObject gameObject, GameObject parentObject, ItemsContainer container)
    {
        Pickupable pickupable = gameObject.RequireComponent<Pickupable>();
        pickupable.Initialize();

        InventoryItem inventoryItem = new(pickupable);

        // Whether the item is lost when its player dies is the item's own state, as in single player: an item picked up in
        // the open stays at risk after a rejoin until the player enters a base or vehicle (see Items.InventorySecured)
        pickupable.destroyOnDeath = entity.DestroyOnDeath;

        bool isPlanter = parentObject.TryGetComponent(out Planter planter);
        bool subscribedValue = false;
        if (isPlanter)
        {
            subscribedValue = planter.subscribed;
            planter.Subscribe(false);
        }

        using (PacketSuppressor<PlayerQuickSlotsBindingChanged>.Suppress())
        using (PacketSuppressor<EntityReparented>.Suppress())
        using (PacketSuppressor<EntityMetadataUpdate>.Suppress())
        using (PacketSuppressor<EntitySpawnedByClient>.Suppress())
        {
            container.UnsafeAdd(inventoryItem);
            Log.Debug($"Received: Added item {pickupable.GetTechType()} ({entity.Id}) to container {parentObject.GetFullHierarchyPath()}");
        }

        if (isPlanter)
        {
            planter.Subscribe(subscribedValue);

            if (entity.Metadata is PlantableMetadata metadata)
            {
                PostponeAddNotification(() => planter.subscribed, () => planter, true, () =>
                {
                    // Adapted from Planter.AddItem(InventoryItem) to be able to call directly AddItem(Plantable, slotID) with our parameters
                    Plantable plantable = pickupable.GetComponent<Plantable>();
                    pickupable.SetTechTypeOverride(plantable.plantTechType, false);
                    inventoryItem.isEnabled = false;

                    Planter.PlantSlot slot = planter.GetSlotByID(metadata.SlotID);
                    if (slot == null || Planters.CanTakeSlot(slot.isOccupied, slot.plantable == plantable))
                    {
                        Plant(planter, plantable, metadata.SlotID, metadata);
                        return;
                    }
                    // This game planted its own seed in that slot at the same moment: the server kept this seed there and
                    // the own seed moves away (Planters.SlotTaken)
                    CoroutineHost.StartCoroutine(PlantOnceSlotIsFree(planter, plantable, slot, metadata));
                });
            }
        }
        else if (parentObject.TryGetComponent(out Trashcan trashcan))
        {
            PostponeAddNotification(() => trashcan.subscribed, () => trashcan, false, () =>
            {
                trashcan.AddItem(inventoryItem);
            });
        }
    }

    private void Plant(Planter planter, Plantable plantable, int slotId, PlantableMetadata metadata)
    {
        planter.AddItem(plantable, slotId);

        // Apply the plantable metadata after the GrowingPlant (or the GrownPlant) is spawned
        // this will allow the GrowingPlant to know about its progress
        entityMetadataManager.ApplyMetadata(plantable.gameObject, metadata);

        // Plant spawning occurs in multiple steps over frames:
        // spawning the item, adding it to the planter, having the GrowingPlant created, and eventually having it create a GrownPlant (when progress == 1)
        // therefore we give the metadata to the object so it can be used when required
        if (plantable.growingPlant && plantable.growingPlant.GetProgress() == 1f)
        {
            plantable.growingPlant.AddReference(metadata);
        }

        // NB: Entities.SpawnBatchAsync (which is the function calling the current spawner)
        // will still apply the metadata another time but we don't care as it's not destructive
    }

    private IEnumerator PlantOnceSlotIsFree(Planter planter, Plantable plantable, Planter.PlantSlot slot, PlantableMetadata metadata)
    {
        float giveUpAt = Time.realtimeSinceStartup + SLOT_WAIT_SECONDS;
        yield return new WaitUntil(() => !planter || !plantable || !slot.isOccupied || Time.realtimeSinceStartup > giveUpAt);
        if (!planter || !plantable || plantable.currentPlanter)
        {
            yield break;
        }
        // A slot still taken (a large and a small plant over each other, which the server cannot tell): any free one
        int slotId = slot.isOccupied ? planter.GetFreeSlotID(plantable.size == Plantable.PlantSize.Large) : metadata.SlotID;
        if (slotId < 0)
        {
            Log.Warn($"[{nameof(InventoryItemEntitySpawner)}] No free slot left in {planter.name} for {plantable.name}");
            yield break;
        }
        using (PacketSuppressor<EntityReparented>.Suppress())
        using (PacketSuppressor<EntityMetadataUpdate>.Suppress())
        {
            Plant(planter, plantable, slotId, metadata);
        }
    }

    private static void PostponeAddNotification(Func<bool> subscribed, Func<bool> instanceValid, bool callbackIfAlreadySubscribed, Action callback)
    {
        IEnumerator PostponedAddCallback()
        {
            yield return new WaitUntil(() => subscribed() || !instanceValid());
            if (instanceValid())
            {
                using (PacketSuppressor<EntityReparented>.Suppress())
                using (PacketSuppressor<EntityMetadataUpdate>.Suppress())
                {
                    callback();
                }
            }
        }

        if (!subscribed())
        {
            CoroutineHost.StartCoroutine(PostponedAddCallback());
        }
        else if (callbackIfAlreadySubscribed)
        {
            callback();
        }
    }
}
