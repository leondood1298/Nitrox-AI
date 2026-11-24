using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Nitrox.Model.DataStructures;
using NitroxClient.Communication;
using NitroxClient.GameLogic.Helper;
using NitroxClient.GameLogic.Spawning.Abstract;
using NitroxClient.GameLogic.Spawning.WorldEntities;
using NitroxClient.MonoBehaviours;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning;

public class InstalledBatteryEntitySpawner : SyncEntitySpawner<InstalledBatteryEntity>
{
    protected override IEnumerator SpawnAsync(InstalledBatteryEntity entity, TaskResult<Optional<GameObject>> result)
    {
        if (!CanSpawn(entity, out EnergyMixin? energyMixin, out string errorLog))
        {
            Log.Error(errorLog);
            result.Set(Optional.Empty);
            yield break;
        }
        // Record this battery as known before waiting on its (potentially multi-frame) prefab spawn, so a pickup
        // happening in that window doesn't wrongly conclude the tool has no battery.
        BatteryChildEntityHelper.RegisterPendingInstalledBattery(entity);

        TaskResult<GameObject> prefabResult = new();
        yield return DefaultWorldEntitySpawner.RequestPrefab(entity.TechType.ToUnity(), prefabResult);
        GameObject gameObject = GameObjectExtensions.InstantiateWithId(prefabResult.Get(), entity.Id);

        SetupObject(gameObject, energyMixin);
        BatteryChildEntityHelper.ForgetPendingInstalledBattery(entity.ParentId, entity.ComponentIndex);

        result.Set(gameObject);
    }

    protected override bool SpawnSync(InstalledBatteryEntity entity, TaskResult<Optional<GameObject>> result)
    {
        if (!DefaultWorldEntitySpawner.TryGetCachedPrefab(out GameObject prefab, entity.TechType.ToUnity()))
        {
            return false;
        }
        if (!CanSpawn(entity, out EnergyMixin energyMixin, out string errorLog))
        {
            Log.Error(errorLog);
            return true;
        }
        BatteryChildEntityHelper.RegisterPendingInstalledBattery(entity);

        GameObject gameObject = GameObjectExtensions.SpawnFromPrefab(prefab, entity.Id);

        SetupObject(gameObject, energyMixin);
        BatteryChildEntityHelper.ForgetPendingInstalledBattery(entity.ParentId, entity.ComponentIndex);

        result.Set(gameObject);
        return true;
    }

    protected override bool SpawnsOwnChildren(InstalledBatteryEntity entity) => false;

    private bool CanSpawn(InstalledBatteryEntity entity, [NotNullWhen(true)] out EnergyMixin? energyMixin, [NotNullWhen(false)] out string? errorLog)
    {
        if (!NitroxEntity.TryGetObjectFrom(entity.ParentId, out GameObject parentObject))
        {
            energyMixin = null;
            errorLog = $"Unable to find parent to install battery {entity}";
            return false;
        }

        energyMixin = parentObject.GetAllComponentsInChildren<EnergyMixin>()
                                  .ElementAtOrDefault(entity.ComponentIndex);

        if (energyMixin == null)
        {
            errorLog = $"Unable to find EnergyMixin on parent to install battery {entity}";
            return false;
        }
        errorLog = null;
        return true;
    }

    private void SetupObject(GameObject gameObject, EnergyMixin energyMixin)
    {
        // Nothing done here is this player's doing: a battery leaving the slot is not taken by them (ItemContainers.BatteryRemoved)
        using (PacketSuppressor<EntityReparented>.Suppress())
        using (PacketSuppressor<EntitySpawnedByClient>.Suppress())
        {
            energyMixin.Initialize();
            energyMixin.RestoreBattery();

            // The server gives a slot to one battery. Another one still in it here is this game's own that lost the slot
            // (its way back is on its way from the server) or one whose removal this game has not heard of: it is
            // removed, else the slot would refuse the new one and leave it lying under the tool, out of reach.
            InventoryItem occupant = energyMixin.batterySlot.storedItem;
            if (occupant != null && occupant.item && occupant.item.gameObject != gameObject)
            {
                Log.Info($"Battery slot of {energyMixin.gameObject.GetFullHierarchyPath()} still held {occupant.item.GetTechType()}, removed for {gameObject.name}");
                Entities.DestroyObject(occupant.item.gameObject);
            }

            energyMixin.batterySlot.AddItem(new InventoryItem(gameObject.GetComponent<Pickupable>()));
        }
    }
}
