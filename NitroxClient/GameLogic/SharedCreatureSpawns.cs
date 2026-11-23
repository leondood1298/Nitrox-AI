using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using Story;
using UnityEngine;
using UWE;

namespace NitroxClient.GameLogic;

/// <summary>
///     Creatures the game makes on its own, outside the world's entities: Warpers (WarperSpawner), the creatures a warp
///     ball brings in (WarpBall.WarpIn), the void's Ghost Leviathans (VoidGhostLeviathansSpawner) and the Sea Emperor
///     Juvenile (JuvenileEmperorSpawner). Every game made its own, which only it saw and which acted on its player alone.
///     Now one game makes each and announces it as an entity, as <see cref="SeaEmperorBabies" /> does for the outside babies;
///     every player sees and meets the same creature, and its end (a Warper warping out, a warped-in creature's time running
///     out, a void Ghost left behind) is sent as its destruction.
/// </summary>
/// <remarks>
///     Who makes a creature at a spawner is the player nearest to it: every game can tell that from the players' positions,
///     and it follows the players as they move, unlike a lock that stays with whoever took it. A void Ghost is made by the
///     game of the player in the void it hunts, as in the game. Unity-bound but for <see cref="IsNearest" /> and
///     <see cref="GetJuvenileId" />.
/// </remarks>
public sealed class SharedCreatureSpawns(SimulationOwnership simulationOwnership, IPacketSender packetSender, Items items, PlayerManager playerManager, Entities entities)
{
    private readonly SimulationOwnership simulationOwnership = simulationOwnership;
    private readonly IPacketSender packetSender = packetSender;
    private readonly Items items = items;
    private readonly PlayerManager playerManager = playerManager;
    private readonly Entities entities = entities;

    /// <summary>
    ///     Whether the local player is at least as near as every other player.
    /// </summary>
    public static bool IsNearest(float localDistance, IEnumerable<float> otherDistances)
    {
        foreach (float otherDistance in otherDistances)
        {
            if (otherDistance < localDistance)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    ///     The id of the juvenile a JuvenileEmperorSpawner makes: the same in every game and every load, so it is made once.
    /// </summary>
    public static NitroxId GetJuvenileId(float spawnerX, float spawnerY, float spawnerZ)
    {
        string key = string.Join(":", "JuvenileEmperorSpawner", Rounded(spawnerX), Rounded(spawnerY), Rounded(spawnerZ));
        using MD5 md5 = MD5.Create();
        return new NitroxId(new System.Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(key))));
    }

    /// <summary>
    ///     Called before a spawner of the game makes a creature: true if this game makes it.
    /// </summary>
    public bool MaySpawnAt(Vector3 position)
    {
        if (!Player.main)
        {
            return false;
        }
        List<float> otherDistances = [];
        foreach (RemotePlayer remotePlayer in playerManager.GetAll())
        {
            if (remotePlayer.Body)
            {
                otherDistances.Add(Vector3.Distance(remotePlayer.Body.transform.position, position));
            }
        }
        return IsNearest(Vector3.Distance(Player.main.transform.position, position), otherDistances);
    }

    /// <summary>
    ///     Announces a creature this game's spawner made. A copy that came as an entity has its id already.
    /// </summary>
    public void Announce(GameObject creature)
    {
        if (!creature || creature.TryGetNitroxId(out _))
        {
            return;
        }
        items.Dropped(creature);
    }

    /// <summary>
    ///     Called when the creature's own game ends it (a Warper warping out, a warped-in creature's time running out).
    /// </summary>
    public void Ending(GameObject creature)
    {
        if (creature && creature.TryGetNitroxId(out NitroxId creatureId) && simulationOwnership.HasAnyLockType(creatureId))
        {
            packetSender.Send(new EntityDestroyed(creatureId));
        }
    }

    /// <summary>
    ///     Whether a game runs a creature's own code that only its simulating player may: a creature nobody shares, or one
    ///     this player simulates.
    /// </summary>
    public bool IsDecidedHere(GameObject creature)
    {
        return !creature || !creature.TryGetNitroxId(out NitroxId creatureId) || simulationOwnership.HasAnyLockType(creatureId);
    }

    /// <summary>
    ///     A warp ball moves a player, a creature or an item. Every game shows the ball of another player's Warper, but only
    ///     its own player is warped there: the rest is moved where the Warper is simulated, and sent from there.
    /// </summary>
    public bool MayWarp(WarpBall warpBall, GameObject target)
    {
        if (IsDecidedHere(warpBall.warperGO))
        {
            return true;
        }
        return Player.main && target == Player.main.gameObject;
    }

    /// <summary>
    ///     Called before GhostLeviatanVoid.UpdateVoidBehaviour, which hunts the game's own player and removes the leviathan
    ///     once that player is far: only the game of the player simulating it runs it, and sends its removal.
    /// </summary>
    public bool MayUpdateVoidGhost(GhostLeviatanVoid ghost)
    {
        if (!IsDecidedHere(ghost.gameObject))
        {
            return false;
        }
        // The game's own test, after which it destroys the leviathan
        if (!Player.main || Vector3.Distance(Player.main.transform.position, ghost.transform.position) > ghost.maxDistanceToPlayer)
        {
            Ending(ghost.gameObject);
        }
        return true;
    }

    /// <summary>
    ///     Replaces JuvenileEmperorSpawner.Start: makes the juvenile once in the world, by the nearest player.
    /// </summary>
    public IEnumerator SpawnJuvenile(JuvenileEmperorSpawner spawner)
    {
        // The completed goals of the world and the entities of the spawner's cell are known once the player is in
        while (!Multiplayer.Main || !Multiplayer.Main.InitialSyncCompleted || entities.SpawningEntities)
        {
            yield return null;
        }
        StoryGoalManager goals = StoryGoalManager.main;
        if (!spawner || !goals || !goals.IsGoalComplete(spawner.listenForBabiesSpawnedOutsideGoal))
        {
            yield break;
        }
        Vector3 position = spawner.transform.position;
        NitroxId juvenileId = GetJuvenileId(position.x, position.y, position.z);
        if (entities.IsKnownEntity(juvenileId) || NitroxEntity.TryGetObjectFrom(juvenileId, out GameObject _) || !MaySpawnAt(position))
        {
            Object.Destroy(spawner.gameObject);
            yield break;
        }

        CoroutineTask<GameObject> task = AddressablesUtility.InstantiateAsync(spawner.juvenileEmporerPrefabReference.RuntimeKey as string, null, position, spawner.transform.rotation, true);
        yield return task;
        GameObject juvenile = task.GetResult();
        if (juvenile)
        {
            NitroxEntity.SetNewId(juvenile, juvenileId);
            items.Dropped(juvenile);
        }
        if (spawner)
        {
            Object.Destroy(spawner.gameObject);
        }
    }

    private static string Rounded(float value)
    {
        return ((int)System.Math.Round(value)).ToString(CultureInfo.InvariantCulture);
    }
}
