using System.Collections;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic.Spawning.Abstract;
using NitroxClient.MonoBehaviours;
using NitroxClient.MonoBehaviours.CinematicController;
using NitroxClient.Unity.Helper;
using Nitrox.Model.Helper;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.WorldEntities;

public class VehicleEntitySpawner(MobileVehicleBay mobileVehicleBay, LavaLarvae lavaLarvae) : EntitySpawner<VehicleEntity>
{
    private readonly MobileVehicleBay bays = mobileVehicleBay;
    private readonly LavaLarvae lavaLarvae = lavaLarvae;

    // The constructor has mixed results when the remote player is a long distance away.  UWE even has a built in distance tracker to ensure
    // that they are within allowed range.  However, this range is a bit restrictive. We will allow constructor spawning up to a specified 
    // distance - anything more will simply use world spawning (no need to play the animation anyways).
    private const float ALLOWED_CONSTRUCTOR_DISTANCE = 100.0f;

    private float RemainingCraftDuration(VehicleEntity vehicleEntity)
    {
        return GetCraftDuration(vehicleEntity.TechType.ToUnity()) - (DayNightCycle.main.timePassedAsFloat - vehicleEntity.ConstructionTime);
    }

    protected override IEnumerator SpawnAsync(VehicleEntity vehicleEntity, TaskResult<Optional<GameObject>> result)
    {
        bool withinConstructorSpawnWindow = (DayNightCycle.main.timePassedAsFloat - vehicleEntity.ConstructionTime) < GetCraftDuration(vehicleEntity.TechType.ToUnity());
        Optional<GameObject> spawnerObj = NitroxEntity.GetObjectFrom(vehicleEntity.SpawnerId);
        Optional<GameObject> parent = vehicleEntity.ParentId != null ? NitroxEntity.GetObjectFrom(vehicleEntity.ParentId) : Optional.Empty;

        if (withinConstructorSpawnWindow && spawnerObj.HasValue && spawnerObj.Value.TryGetComponent(out Constructor constructor))
        {
            float distance = (constructor.transform.position - Player.main.transform.position).magnitude;
            if (distance <= ALLOWED_CONSTRUCTOR_DISTANCE)
            {
                yield return SpawnViaConstructor(vehicleEntity, constructor, result);
                yield break;
            }
            // Too far for the animation; the bay is still busy until the build is done, as in the game
            ConstructorInput crafter = constructor.gameObject.RequireComponentInChildren<ConstructorInput>(true);
            bays.MarkBuilding(crafter, vehicleEntity.TechType.ToUnity(), RemainingCraftDuration(vehicleEntity));
        }

        yield return SpawnInWorld(vehicleEntity, result, parent);
    }

    protected override bool SpawnsOwnChildren(VehicleEntity entity) => false;

    private IEnumerator SpawnInWorld(VehicleEntity vehicleEntity, TaskResult<Optional<GameObject>> result, Optional<GameObject> parent)
    {
        TechType techType = vehicleEntity.TechType.ToUnity();
        GameObject gameObject = null;
        Vehicle vehicle = null;

        bool isCyclops = techType == TechType.Cyclops;
        if (isCyclops)
        {
            GameObject prefab = null;
            LightmappedPrefabs.main.RequestScenePrefab("cyclops", (go) => prefab = go);
            yield return new WaitUntil(() => prefab != null);
            SubConsoleCommand.main.OnSubPrefabLoaded(prefab);
            gameObject = SubConsoleCommand.main.GetLastCreatedSub();
        }
        else
        {
            CoroutineTask<GameObject> techPrefabCoroutine = CraftData.GetPrefabForTechTypeAsync(techType, false);
            yield return techPrefabCoroutine;
            GameObject techPrefab = techPrefabCoroutine.GetResult();
            gameObject = Utils.SpawnPrefabAt(techPrefab, null, vehicleEntity.Transform.Position.ToUnity());
            Validate.NotNull(gameObject, $"{nameof(VehicleEntitySpawner)}: No prefab for tech type: {techType}");

            if (gameObject.TryGetComponent(out vehicle))
            {
                vehicle.LazyInitialize();
            }
        }

        AddCinematicControllers(gameObject);

        gameObject.transform.position = vehicleEntity.Transform.Position.ToUnity();
        gameObject.transform.rotation = vehicleEntity.Transform.Rotation.ToUnity();
        gameObject.SetActive(true);
        gameObject.SendMessage("StartConstruction", SendMessageOptions.DontRequireReceiver);

        CrafterLogic.NotifyCraftEnd(gameObject, CraftData.GetTechType(gameObject));
        Rigidbody rigidBody = gameObject.RequireComponent<Rigidbody>();
        rigidBody.isKinematic = false;

        yield return Yielders.WaitForEndOfFrame;

        RemoveConstructionAnimations(gameObject);

        yield return Yielders.WaitForEndOfFrame;

        Vehicles.RemoveNitroxEntitiesTagging(gameObject);

        NitroxEntity.SetNewId(gameObject, vehicleEntity.Id);

        if (vehicleEntity.Metadata is CyclopsMetadata cyclopsMetadata && cyclopsMetadata.IsDestroyed)
        {
            // Swap to destroyed look without triggering animations / effects
            gameObject.BroadcastMessage("SwapToDamagedModels");
            gameObject.BroadcastMessage("OnKill");
            gameObject.BroadcastMessage("CyclopsDeathEvent", SendMessageOptions.DontRequireReceiver);
        }

        if (parent.HasValue)
        {
            DockVehicle(gameObject, parent.Value);
        }
        
        // While spawning a vehicle, we want to make sure that it doesn't free fall as if it was just built by a constructor
        if (gameObject.TryGetComponent(out vehicle))
        {
            vehicle.constructionFallOverride = false;
        }

        // Lava Larvae loaded before their vehicle go on it now
        lavaLarvae.VehicleSpawned(vehicleEntity.Id, gameObject);

        result.Set(gameObject);
    }

    private IEnumerator SpawnViaConstructor(VehicleEntity vehicleEntity, Constructor constructor, TaskResult<Optional<GameObject>> result)
    {
        if (!constructor.deployed)
        {
            constructor.Deploy(true);
        }

        float craftDuration = RemainingCraftDuration(vehicleEntity);
        TechType techType = vehicleEntity.TechType.ToUnity();
        ConstructorInput crafter = constructor.gameObject.RequireComponentInChildren<ConstructorInput>(true);

        // The vehicle the game makes at this bay gets the entity's id there (MobileVehicleBay.BeginCrafting); asking for it
        // by id keeps a build made meanwhile, here or at another bay, from being taken for this one
        bays.BeginReplay(crafter, vehicleEntity.Id, techType, craftDuration);
        GameObject constructedObject;
        try
        {
            yield return crafter.OnCraftingBeginAsync(techType, craftDuration);
        }
        finally
        {
            constructedObject = bays.EndReplay(crafter, vehicleEntity.Id);
        }
        Validate.IsTrue(constructedObject, $"Could not find constructed object from MobileVehicleBay {constructor.gameObject.name}");

        AddCinematicControllers(constructedObject);

        result.Set(constructedObject);
        yield break;
    }

    /// <summary>
    ///   For scene objects like cyclops, PlayerCinematicController Start() will not be called to add Cinematic reference.
    /// </summary>
    private void AddCinematicControllers(GameObject gameObject)
    {
        if (gameObject.GetComponent<MultiplayerCinematicReference>())
        {
            return;
        }

        PlayerCinematicController[] controllers = gameObject.GetComponentsInChildren<PlayerCinematicController>(true);

        if (controllers.Length == 0)
        {
            return;
        }

        MultiplayerCinematicReference reference = gameObject.AddComponent<MultiplayerCinematicReference>();

        foreach (PlayerCinematicController controller in controllers)
        {
            reference.AddController(controller);
        }
    }

    /// <summary>
    ///  When loading in vehicles, they still briefly have their blue crafting animation playing.  Force them to stop.
    /// </summary>
    private void RemoveConstructionAnimations(GameObject gameObject)
    {
        VFXConstructing[] vfxConstructions = gameObject.GetComponentsInChildren<VFXConstructing>();
        
        foreach (VFXConstructing vfxConstructing in vfxConstructions)
        {
            vfxConstructing.EndGracefully();
        }
    }

    private void DockVehicle(GameObject gameObject, GameObject parent)
    {
        Vehicle vehicle = gameObject.GetComponent<Vehicle>();

        if (!vehicle)
        {
            Log.Info($"Could not find vehicle component on docked vehicle {gameObject.name}");
            return;
        }

        VehicleDockingBay dockingBay = parent.GetComponentInChildren<VehicleDockingBay>(true);

        if (!dockingBay)
        {
            Log.Info($"Could not find VehicleDockingBay component on dock object {parent.name}");
            return;
        }

        // Not DockVehicle: it would also play the local player's docking goal ("Dock_Seamoth") for every docked vehicle loaded
        Vehicles.DockWithoutPlayer(dockingBay, vehicle);
    }
    
    private float GetCraftDuration(TechType techType)
    {
        // UWE hard codes the build times into if/else logic inside ConstructorInput.Craft().

        switch(techType)
        {
            case TechType.Seamoth:
                return 10f;
            case TechType.Exosuit:
                return 10f;
            case TechType.Cyclops:
                return 20f;
            case TechType.RocketBase:
                return 25f;
        }

        return 10f;
    }
}
