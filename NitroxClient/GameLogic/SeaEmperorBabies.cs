using System.Collections;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Simulation;
using NitroxClient.MonoBehaviours;
using Story;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     The Sea Emperor babies that appear in the world after leaving the prison (SeaEmperorBabiesSpawner), and what any
///     baby that is an entity drops.<br />
///     The game makes the outside babies once, when the spawner first loads after they left, and saves them. Here they are
///     made once for everybody as entities:<br />
///     - one player at a time makes them: the one holding the exclusive lock on the spawner's claim id. The others wait
///     until the babies are made (the spawner's goal is shared); if the maker leaves before it is done, the lock is free and
///     the next player makes the rest<br />
///     - each baby (and the cure ball it drops at once) has an id fixed by its spawn point, so a baby that is made again by
///     the next player is the same entity, never a second one<br />
///     - a baby's size and drops are its metadata, so that whoever simulates it drops no more than the game allows
/// </summary>
public class SeaEmperorBabies
{
    /// <summary>
    ///     Seconds a player who could not take the claim waits before trying again.
    /// </summary>
    private const float CLAIM_RETRY_INTERVAL = 5f;

    private readonly Entities entities;
    private readonly SimulationOwnership simulationOwnership;
    private readonly Items items;
    private readonly CreatureInfections creatureInfections;

    private readonly HashSet<NitroxId> outsideBabyIds = [];

    public SeaEmperorBabies(Entities entities, SimulationOwnership simulationOwnership, Items items, CreatureInfections creatureInfections)
    {
        this.entities = entities;
        this.simulationOwnership = simulationOwnership;
        this.items = items;
        this.creatureInfections = creatureInfections;
    }

    /// <summary>
    ///     Replaces SeaEmperorBabiesSpawner.Start.
    /// </summary>
    public IEnumerator Spawn(SeaEmperorBabiesSpawner spawner)
    {
        // The completed goals of the world are known once the player is in
        while (!Multiplayer.Main || !Multiplayer.Main.InitialSyncCompleted)
        {
            yield return null;
        }
        StoryGoalManager goals = StoryGoalManager.main;
        if (!goals || !goals.IsGoalComplete(spawner.listenForBabiesLeftPrisonGoal))
        {
            // Like the game: the babies appear the next time the spawner loads after they left
            yield break;
        }

        Vector3 spawnerPosition = spawner.transform.position;
        NitroxId claimId = SeaEmperorBabyRules.GetClaimId(spawnerPosition.x, spawnerPosition.y, spawnerPosition.z);
        while (spawner && !goals.IsGoalComplete(spawner.babiesSpawnedGoal.key))
        {
            if (!simulationOwnership.HasExclusiveLock(claimId))
            {
                bool? acquired = null;
                simulationOwnership.RequestSimulationLock(new LockRequest<SpawnerClaim>(claimId, SimulationLockType.EXCLUSIVE, (_, lockAcquired, _) => acquired = lockAcquired, new SpawnerClaim()));
                while (acquired == null)
                {
                    yield return null;
                }
                if (!acquired.Value)
                {
                    // Somebody else is making them
                    yield return new WaitForSeconds(CLAIM_RETRY_INTERVAL);
                    continue;
                }
            }

            // Babies made earlier come with the spawner's cell: they must not be made again
            while (entities.SpawningEntities)
            {
                yield return null;
            }
            bool allMade = true;
            for (int i = 0; i < spawner.spawnPoints.Length; i++)
            {
                if (!spawner || !simulationOwnership.HasExclusiveLock(claimId))
                {
                    allMade = false;
                    break;
                }
                NitroxId babyId = SeaEmperorBabyRules.GetBabyId(spawnerPosition.x, spawnerPosition.y, spawnerPosition.z, i);
                if (IsPresent(babyId))
                {
                    continue;
                }
                NitroxId ballId = SeaEmperorBabyRules.GetCureBallId(spawnerPosition.x, spawnerPosition.y, spawnerPosition.z, i);
                yield return MakeBaby(spawner, spawner.spawnPoints[i], babyId, ballId);
                allMade &= IsPresent(babyId);
            }

            if (spawner && allMade)
            {
                // Shared with the others, whose spawners then stop waiting; and with joiners, whose spawners make nothing
                spawner.babiesSpawnedGoal.Trigger();
                break;
            }
            yield return new WaitForSeconds(CLAIM_RETRY_INTERVAL);
        }

        if (spawner)
        {
            Object.Destroy(spawner.gameObject);
        }
    }

    /// <summary>
    ///     Called before SeaEmperorBaby_Start_Patch makes a baby swim to its mother: an outside baby has no business there.
    /// </summary>
    public bool IsOutsideBaby(SeaEmperorBaby baby)
    {
        return baby.TryGetNitroxId(out NitroxId babyId) && outsideBabyIds.Contains(babyId);
    }

    /// <summary>
    ///     Called before DropEnzymes.Update. A baby that is an entity drops on the timer of its simulating player only, so
    ///     that the count of its drops is one count.
    /// </summary>
    public bool MayDrop(DropEnzymes dropEnzymes)
    {
        SeaEmperorBaby baby = dropEnzymes.GetComponentInParent<SeaEmperorBaby>();
        return !baby || !baby.TryGetNitroxId(out NitroxId babyId) || simulationOwnership.HasAnyLockType(babyId);
    }

    /// <summary>
    ///     Called after DropEnzymes.Update counted a drop.
    /// </summary>
    public void OnDropped(DropEnzymes dropEnzymes)
    {
        SeaEmperorBaby baby = dropEnzymes.GetComponentInParent<SeaEmperorBaby>();
        if (baby && baby.TryGetNitroxId(out NitroxId babyId) && outsideBabyIds.Contains(babyId) && simulationOwnership.HasAnyLockType(babyId))
        {
            entities.BroadcastMetadataUpdate(babyId, Extract(baby));
        }
    }

    public SeaEmperorBabyMetadata? Extract(SeaEmperorBaby baby)
    {
        if (!IsOutsideBaby(baby))
        {
            return null;
        }
        DropEnzymes dropEnzymes = baby.dropEnzymes;
        return new SeaEmperorBabyMetadata(baby.transform.localScale.x, dropEnzymes.spawnCount, dropEnzymes.spawnLimit, creatureInfections.GetDecidedAmount(baby),
                                          creatureInfections.GetDecidedEnzymeEndTime(baby));
    }

    public void Apply(GameObject gameObject, SeaEmperorBabyMetadata metadata)
    {
        if (!gameObject.TryGetComponent(out SeaEmperorBaby baby))
        {
            Log.Error($"[{nameof(SeaEmperorBabies)}] Could not find {nameof(SeaEmperorBaby)} on {gameObject.name}");
            return;
        }
        if (gameObject.TryGetNitroxId(out NitroxId babyId))
        {
            outsideBabyIds.Add(babyId);
        }
        // What SeaEmperorBabiesSpawner sets up, with the drops made so far
        baby.SetScale(metadata.Scale);
        DropEnzymes dropEnzymes = baby.dropEnzymes;
        dropEnzymes.spawnLimit = metadata.DropLimit;
        dropEnzymes.spawnCount = metadata.DropCount;
        dropEnzymes.enabled = SeaEmperorBabyRules.StillDrops(metadata.DropCount, metadata.DropLimit);
        creatureInfections.ApplyReceived(gameObject, metadata);
    }

    private IEnumerator MakeBaby(SeaEmperorBabiesSpawner spawner, Transform spawnPoint, NitroxId babyId, NitroxId ballId)
    {
        CoroutineTask<GameObject> babyTask = AddressablesUtility.InstantiateAsync(spawner.babyEmporerPrefabReference.RuntimeKey as string, null, spawnPoint.position, spawnPoint.rotation, true);
        yield return babyTask;
        GameObject babyObject = babyTask.GetResult();
        if (!babyObject)
        {
            yield break;
        }
        // Arrived from the server meanwhile
        if (IsPresent(babyId) || !babyObject.TryGetComponent(out SeaEmperorBaby baby))
        {
            Object.Destroy(babyObject);
            yield break;
        }

        // Before its Start, which must not send it to its mother and must find that we simulate it
        NitroxEntity.SetNewId(babyObject, babyId);
        outsideBabyIds.Add(babyId);
        simulationOwnership.TakeOwnership(babyId, SimulationLockType.TRANSIENT, true);

        // What SeaEmperorBabiesSpawner does
        baby.SetScale(spawner.babyScale);
        baby.dropEnzymes.enabled = true;
        baby.dropEnzymes.spawnLimit = SeaEmperorBabyRules.OUTSIDE_DROP_LIMIT;
        GameObject ball = null;
        if (!IsPresent(ballId))
        {
            Vector3 ballPosition = baby.transform.position - baby.transform.forward * 3f;
            CoroutineTask<GameObject> ballTask = AddressablesUtility.InstantiateAsync(baby.dropEnzymes.enzymePrefabReference.RuntimeKey as string, null, ballPosition, Quaternion.identity, true);
            yield return ballTask;
            ball = ballTask.GetResult();
        }
        LargeWorldEntity.Register(babyObject);

        // The ball before the baby: a baby that is announced is never made again, a ball made again keeps its id
        if (ball)
        {
            LargeWorldEntity.Register(ball);
            NitroxEntity.SetNewId(ball, ballId);
            items.Dropped(ball, TechType.EnzymeCureBall);
        }

        if (!babyObject.TryGetComponent(out PrefabIdentifier prefabIdentifier))
        {
            Log.Error($"[{nameof(SeaEmperorBabies)}] Sea Emperor baby {babyId} has no {nameof(PrefabIdentifier)}");
            yield break;
        }
        int level = babyObject.TryGetComponent(out LargeWorldEntity largeWorldEntity) ? (int)largeWorldEntity.cellLevel : 0;
        WorldEntity entity = new(babyObject.transform.ToWorldDto(), level, prefabIdentifier.ClassId, false, babyId, TechType.SeaEmperorBaby.ToDto(), Extract(baby), null, []);
        entities.BroadcastEntitySpawnedByClient(entity);
        entities.MarkAsSpawned(entity);
    }

    private bool IsPresent(NitroxId entityId)
    {
        return entities.IsKnownEntity(entityId) || NitroxEntity.TryGetObjectFrom(entityId, out GameObject _);
    }

    private sealed class SpawnerClaim : LockRequestContext
    {
    }
}
