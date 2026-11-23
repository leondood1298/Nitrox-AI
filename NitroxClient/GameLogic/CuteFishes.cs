using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     A Cuddlefish follows, or stays for, whoever last told it to (CuteFishHandTarget), and keeps that through a restart
///     (<see cref="CuteFishMetadata" />). The command was a local toggle: the others' copies kept doing what they did, it was
///     lost when the fish loaded again, and the fish followed only its simulating player, whoever had told it, while every
///     game warped its copy next to its own player.
/// </summary>
/// <remarks>Unity-bound.</remarks>
public sealed class CuteFishes(Entities entities, SimulationOwnership simulationOwnership, PlayerManager playerManager, CreatureInfections creatureInfections)
{
    private readonly Entities entities = entities;
    private readonly SimulationOwnership simulationOwnership = simulationOwnership;
    private readonly PlayerManager playerManager = playerManager;
    private readonly CreatureInfections creatureInfections = creatureInfections;

    private readonly Dictionary<NitroxId, NitroxId> leaderIdByFishId = [];

    /// <summary>
    ///     Called after CuteFishHandTarget.PlayCommandAnimation toggled whether the fish follows: this player told it.
    /// </summary>
    public void Commanded(CuteFish cuteFish)
    {
        if (!cuteFish.TryGetNitroxId(out NitroxId fishId) || !Player.main || !Player.main.TryGetNitroxId(out NitroxId playerId))
        {
            return;
        }
        leaderIdByFishId[fishId] = playerId;
        entities.BroadcastMetadataUpdate(fishId, new CuteFishMetadata(cuteFish.followingPlayer, Optional.Of(playerId), creatureInfections.GetDecidedAmount(cuteFish),
                                                                      creatureInfections.GetDecidedEnzymeEndTime(cuteFish)));
    }

    public void ApplyState(CuteFish cuteFish, CuteFishMetadata metadata)
    {
        if (cuteFish.TryGetNitroxId(out NitroxId fishId))
        {
            if (metadata.LeaderId.HasValue)
            {
                leaderIdByFishId[fishId] = metadata.LeaderId.Value;
            }
            else
            {
                leaderIdByFishId.Remove(fishId);
            }
        }
        if (cuteFish.followingPlayer != metadata.FollowingPlayer)
        {
            cuteFish.followingPlayer = metadata.FollowingPlayer;
        }
    }

    /// <summary>
    ///     Called before CreatureFollowPlayer.ScheduledUpdate, which puts the leash in front of the game's own player. Only the
    ///     fish's simulating game moves it; it follows its leader, who may be another player.
    /// </summary>
    public bool MayFollowLocalPlayer(CreatureFollowPlayer follow)
    {
        Creature creature = follow.creature;
        if (!creature || !creature.TryGetNitroxId(out NitroxId creatureId))
        {
            return true;
        }
        if (!simulationOwnership.HasAnyLockType(creatureId))
        {
            return false;
        }
        if (!TryGetRemoteLeader(creatureId, out GameObject leaderBody))
        {
            return true;
        }
        if (Time.time > follow.timeNextUpdate)
        {
            follow.timeNextUpdate = Time.time + follow.updateInterval;
            Vector3 leash = leaderBody.transform.position + leaderBody.transform.forward * follow.distanceToPlayer;
            leash.y = Mathf.Min(leash.y, follow.maxYPos);
            creature.leashPosition = leash;
        }
        return false;
    }

    /// <summary>
    ///     Called before CuteFish.WarpToPlayer, which brings a following fish next to the game's own player when it is far:
    ///     only on the fish's simulating game, and only to that player when it is the leader.
    /// </summary>
    public bool MayWarpToLocalPlayer(CuteFish cuteFish)
    {
        if (!cuteFish.TryGetNitroxId(out NitroxId fishId))
        {
            return true;
        }
        return simulationOwnership.HasAnyLockType(fishId) && !TryGetRemoteLeader(fishId, out _);
    }

    private bool TryGetRemoteLeader(NitroxId fishId, out GameObject leaderBody)
    {
        leaderBody = null;
        if (!leaderIdByFishId.TryGetValue(fishId, out NitroxId leaderId) || (Player.main && Player.main.TryGetNitroxId(out NitroxId localPlayerId) && localPlayerId == leaderId))
        {
            return false;
        }
        Optional<RemotePlayer> leader = playerManager.Find(leaderId);
        if (!leader.HasValue || !leader.Value.Body)
        {
            return false;
        }
        leaderBody = leader.Value.Body;
        return true;
    }
}
