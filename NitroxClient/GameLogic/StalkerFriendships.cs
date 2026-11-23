using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Lets a Stalker befriend any player, not only the one simulating it. The game befriends a player who feeds the
///     Stalker by hand (Stalker.OnFishEat) or whose dropped item it picks up (Stalker.OnShinyPickUp reads the item's
///     Smell, which Pickupable.Drop gives it). Both only worked for the simulating player:<br />
///     - another player's dropped item had no Smell on the simulating game; the dropping game now sends it
///     (<see cref="DroppedItemSmell" />) and the Stalker finds it when it picks the item up<br />
///     - another player's hand-feeding only reached their own copy of the Stalker; it is now sent to the simulating game
///     (<see cref="StalkerFedByPlayer" />), which feeds its Stalker like the game does, a hero peeper's healing included<br />
///     The friendship itself (CreatureFriend), which the game keeps in its save, is the Stalker's metadata
///     (<see cref="StalkerMetadata" />): it survives a change of simulating player and a server restart. A friend whose
///     body is not there yet when the Stalker spawns becomes its friend when the body appears.
/// </summary>
/// <remarks>
///     The game does not save the Friendliness trait (Creature.Start sets it from initialFriendliness on every load), so
///     neither does this class.
/// </remarks>
public sealed class StalkerFriendships
{
    /// <summary>
    ///     MeleeAttack.TryEat: a medium fish feeds 1.5 times as much as a small one, a shark-sized one 3 times.
    /// </summary>
    private const float MEDIUM_FISH_FOOD = 1.5f;

    private const float SHARK_FOOD = 3f;

    /// <summary>
    ///     What MeleeAttack.TryEat heals a creature's infection by when it eats a hero peeper (InfectedMixin.Heal).
    /// </summary>
    private const float HERO_PEEPER_HEAL = 0.5f;

    private readonly IPacketSender packetSender;
    private readonly SimulationOwnership simulationOwnership;
    private readonly StalkerMetadataExtractor metadataExtractor;

    private readonly Dictionary<NitroxId, RememberedSmell> smellsByItemId = [];

    /// <summary>
    ///     Stalkers whose friend's body was not there when their friendship was applied.
    /// </summary>
    private readonly Dictionary<NitroxId, PendingFriendship> pendingByStalkerId = [];

    private bool applying;

    public StalkerFriendships(IPacketSender packetSender, SimulationOwnership simulationOwnership, StalkerMetadataExtractor metadataExtractor, PlayerManager playerManager)
    {
        this.packetSender = packetSender;
        this.simulationOwnership = simulationOwnership;
        this.metadataExtractor = metadataExtractor;
        playerManager.OnBodyCreated += remotePlayer => FriendAppeared(remotePlayer.PlayerContext.PlayerNitroxId);
    }

    /// <summary>
    ///     Called after CreatureFriend.SetFriend, which every change of friend goes through (including Unfriend).
    /// </summary>
    public void FriendChanged(CreatureFriend creatureFriend)
    {
        if (applying || !creatureFriend.TryGetComponent(out Stalker stalker) || !stalker.TryGetNitroxId(out NitroxId stalkerId) ||
            !simulationOwnership.HasAnyLockType(stalkerId))
        {
            return;
        }
        StalkerMetadata metadata = metadataExtractor.Extract(stalker);
        if (metadata != null)
        {
            packetSender.Send(new EntityMetadataUpdate(stalkerId, metadata));
        }
    }

    /// <summary>
    ///     Called by the metadata processor, when the Stalker spawns and when its simulating player's Stalker changes friend.
    /// </summary>
    public void ApplyState(Stalker stalker, StalkerMetadata metadata)
    {
        if (!stalker.TryGetNitroxId(out NitroxId stalkerId))
        {
            return;
        }
        double now = DayNightCycle.main ? DayNightCycle.main.timePassed : 0d;
        bool befriended = metadata.FriendId.HasValue && metadata.FriendshipEndTime > now;
        pendingByStalkerId.Remove(stalkerId);

        GameObject friend = null;
        if (befriended && !NitroxEntity.TryGetObjectFrom(metadata.FriendId.Value, out friend))
        {
            // The friend's body is not there yet: another player still loading or not connected
            RememberPendingFriend(stalkerId, metadata.FriendId.Value, metadata.FriendshipEndTime);
        }
        SetFriend(stalker, friend, metadata.FriendshipEndTime - now);
    }

    /// <summary>
    ///     A remote player's body appeared: the Stalkers waiting for it as their friend befriend it now.
    /// </summary>
    public void FriendAppeared(NitroxId playerId)
    {
        if (!DayNightCycle.main || !NitroxEntity.TryGetObjectFrom(playerId, out GameObject friend))
        {
            return;
        }
        foreach ((NitroxId stalkerId, double remaining) in TakePendingFriendships(playerId, DayNightCycle.main.timePassed))
        {
            if (NitroxEntity.TryGetComponentFrom(stalkerId, out Stalker stalker))
            {
                SetFriend(stalker, friend, remaining);
            }
        }
    }

    /// <summary>
    ///     Called before Stalker.OnFishEat. A Stalker that eats from the local player's hand while another player simulates
    ///     it only does so on this game.
    /// </summary>
    public void FishEaten(Stalker stalker, GameObject fish)
    {
        if (!fish || !fish.GetComponentInParent<Player>() || !stalker.TryGetNitroxId(out NitroxId stalkerId) || simulationOwnership.HasAnyLockType(stalkerId) ||
            !Player.mainObject || !Player.mainObject.TryGetNitroxId(out NitroxId playerId))
        {
            return;
        }
        bool heroPeeper = fish.TryGetComponent(out Peeper peeper) && peeper.isHero;
        packetSender.Send(new StalkerFedByPlayer(stalkerId, playerId, GetFoodValue(CreatureData.GetBehaviourType(fish)), heroPeeper));
    }

    /// <summary>
    ///     What MeleeAttack.TryEat and Stalker.OnFishEat do on the game that ate the fish, on the simulating game.
    /// </summary>
    public void ApplyFed(StalkerFedByPlayer fed)
    {
        if (!simulationOwnership.HasAnyLockType(fed.StalkerId) || !NitroxEntity.TryGetComponentFrom(fed.StalkerId, out Stalker stalker))
        {
            return;
        }
        // The feeder's game could not heal it: only this game changes the Stalker's infection (CreatureInfections), and
        // sends the result
        if (fed.HeroPeeper && stalker.TryGetComponent(out InfectedMixin infectedMixin))
        {
            infectedMixin.Heal(HERO_PEEPER_HEAL);
        }
        if (stalker.TryGetComponent(out MeleeAttack meleeAttack))
        {
            stalker.Hunger.Add(-meleeAttack.eatHungerDecrement * fed.FoodValue);
            stalker.Happy.Add(meleeAttack.eatHappyIncrement * fed.FoodValue);
        }
        if (NitroxEntity.TryGetObjectFrom(fed.PlayerId, out GameObject playerBody))
        {
            stalker.Befriend(playerBody, stalker.feedFriendlinessIncrement);
        }
    }

    public void RememberSmell(DroppedItemSmell smell)
    {
        RememberSmell(smell.ItemId, smell.PlayerId, smell.Strength, smell.Falloff, Time.time);
    }

    /// <summary>
    ///     Called before Stalker.OnShinyPickUp reads the Smell of what it picked up.
    /// </summary>
    public void ShinyPickingUp(object target)
    {
        if (target is not GameObject item || !item || item.GetComponent<Smell>() || !item.TryGetNitroxId(out NitroxId itemId) ||
            !TryGetSmell(itemId, Time.time, out NitroxId playerId, out float strength, out float falloff) ||
            !NitroxEntity.TryGetObjectFrom(playerId, out GameObject playerBody))
        {
            return;
        }
        Smell smell = item.AddComponent<Smell>();
        smell.owner = playerBody;
        smell.strength = strength;
        smell.falloff = falloff;
    }

    internal static float GetFoodValue(BehaviourType fishType)
    {
        return fishType switch
        {
            BehaviourType.MediumFish => MEDIUM_FISH_FOOD,
            BehaviourType.Shark => SHARK_FOOD,
            _ => 1f
        };
    }

    internal void RememberPendingFriend(NitroxId stalkerId, NitroxId friendId, double friendshipEndTime)
    {
        pendingByStalkerId[stalkerId] = new PendingFriendship(friendId, friendshipEndTime);
    }

    /// <returns>The Stalkers waiting for this friend and how long their friendship has left; those that ended are forgotten.</returns>
    internal List<(NitroxId StalkerId, double Remaining)> TakePendingFriendships(NitroxId friendId, double now)
    {
        List<(NitroxId, double)> friendships = [];
        List<NitroxId> taken = [];
        foreach (KeyValuePair<NitroxId, PendingFriendship> pair in pendingByStalkerId)
        {
            bool ended = pair.Value.FriendshipEndTime <= now;
            if (pair.Value.FriendId == friendId || ended)
            {
                taken.Add(pair.Key);
                if (!ended)
                {
                    friendships.Add((pair.Key, pair.Value.FriendshipEndTime - now));
                }
            }
        }
        foreach (NitroxId stalkerId in taken)
        {
            pendingByStalkerId.Remove(stalkerId);
        }
        return friendships;
    }

    private void SetFriend(Stalker stalker, GameObject friend, double remaining)
    {
        applying = true;
        try
        {
            if (friend && remaining > 0d)
            {
                stalker.SetFriend(friend, (float)remaining);
            }
            else if (stalker.creatureFriend && stalker.creatureFriend.GetFriend())
            {
                stalker.creatureFriend.Unfriend();
            }
        }
        finally
        {
            applying = false;
        }
    }

    internal void RememberSmell(NitroxId itemId, NitroxId playerId, float strength, float falloff, float now)
    {
        // A smell is gone within seconds (Smell.Update); those nobody asked for are not kept
        List<NitroxId> faded = [];
        foreach (KeyValuePair<NitroxId, RememberedSmell> pair in smellsByItemId)
        {
            if (pair.Value.StrengthAt(now) <= 0f)
            {
                faded.Add(pair.Key);
            }
        }
        foreach (NitroxId fadedId in faded)
        {
            smellsByItemId.Remove(fadedId);
        }
        smellsByItemId[itemId] = new RememberedSmell(playerId, strength, falloff, now);
    }

    /// <returns>False if the item was not dropped by another player, or its smell has faded.</returns>
    internal bool TryGetSmell(NitroxId itemId, float now, out NitroxId playerId, out float strength, out float falloff)
    {
        playerId = null;
        strength = 0f;
        falloff = 0f;
        if (!smellsByItemId.TryGetValue(itemId, out RememberedSmell smell))
        {
            return false;
        }
        strength = smell.StrengthAt(now);
        if (strength <= 0f)
        {
            smellsByItemId.Remove(itemId);
            return false;
        }
        playerId = smell.PlayerId;
        falloff = smell.Falloff;
        return true;
    }

    private readonly record struct PendingFriendship(NitroxId FriendId, double FriendshipEndTime);

    private readonly record struct RememberedSmell(NitroxId PlayerId, float Strength, float Falloff, float Time)
    {
        /// <summary>
        ///     Smell.Update takes falloff from the strength every second.
        /// </summary>
        public float StrengthAt(float now) => Strength - Falloff * (now - Time);
    }
}
