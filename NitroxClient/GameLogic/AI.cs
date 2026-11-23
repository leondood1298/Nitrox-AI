using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;
using static Nitrox.Model.Subnautica.Packets.RangedAttackLastTargetUpdate;

namespace NitroxClient.GameLogic;

public sealed class AI
{
    private readonly IPacketSender packetSender;
    private readonly Dictionary<Creature, CreatureAction> actions = [];
    private readonly Dictionary<string, Type> cachedCreatureActionTypeByFullName;

    /// <summary>
    /// Contains the types derived from CreatureAction which should be synced.
    /// Actions concerning the creature movement should be ignored as it's already done through SplineFollowing
    /// </summary>
    private readonly HashSet<Type> creatureActionWhitelist =
    [
        typeof(AttackLastTarget), typeof(RangedAttackLastTarget), typeof(AttackCyclops), typeof(Poop),
        // Their only effect beyond swimming is the creature's "attacking" pose while it goes for the meat or the peeper
        typeof(SwimToMeat), typeof(SwimToHeroPeeper)
    ];

    /// <summary>
    /// In the future, ensure all creatures are synced. We want each of them to be individually
    /// checked (that all their actions are synced) before marking them as synced.
    /// </summary>
    private readonly HashSet<Type> syncedCreatureWhitelist =
    [
        typeof(ReaperLeviathan), typeof(SeaDragon), typeof(SeaTreader), typeof(GhostLeviathan),
        // Predators whose only behaviour beyond swimming is to attack what they see. Judged from their classes, not yet
        // confirmed in game: without this they can never bite a player who is not simulating them.
        typeof(BoneShark), typeof(Biter), typeof(SpineEel), typeof(Shocker),
        // Their other behaviour has its own sync: what a Stalker carries (StalkerShinyGrabs), a Sand Shark's burrow (SandSharkBurrows)
        typeof(Stalker), typeof(SandShark)
    ];

    /// <summary>
    ///     Synced creatures whose bites stay with their simulating player's game, which passes a bite on another player's
    ///     body on to that player (<see cref="CreatureAttacksOnPlayers" />). Every game sees what they aim at and how they
    ///     attack, but a bite counts where the simulating player saw it land.
    /// </summary>
    private readonly HashSet<Type> reportedBiteCreatures =
    [
        typeof(Stalker), typeof(SandShark)
    ];

    /// <summary>
    ///     A creature's action is repeated this often even if it did not change, for players who loaded the creature
    ///     after it was last sent.
    /// </summary>
    private const float ACTION_RESEND_SECONDS = 5f;

    private readonly CreatureActionBroadcastFilter actionBroadcastFilter = new(ACTION_RESEND_SECONDS);

    public AI(IPacketSender packetSender)
    {
        this.packetSender = packetSender;
        Assembly assembly = Assembly.GetAssembly(typeof(CreatureAction));
        cachedCreatureActionTypeByFullName = assembly.GetTypes()
                                                     .Where(type => typeof(CreatureAction).IsAssignableFrom(type))
                                                     .ToDictionary(t => t.FullName, t => t);
    }

    public void BroadcastNewAction(NitroxId creatureId, Creature creature, CreatureAction newAction)
    {
        // ChooseBestAction returns nothing while none of the creature's actions wants to run.
        if (!newAction || !syncedCreatureWhitelist.Contains(creature.GetType()))
        {
            return;
        }

        string actionTypeName = newAction.GetType().FullName;
        if (actionBroadcastFilter.ShouldSend(creatureId, actionTypeName, Time.realtimeSinceStartup))
        {
            packetSender.Send(new CreatureActionChanged(creatureId, actionTypeName));
        }
    }

    public void CreatureActionChanged(NitroxId id, string creatureActionTypeName)
    {
        if (!NitroxEntity.TryGetComponentFrom(id, out Creature creature))
        {
            return;
        }
        if (cachedCreatureActionTypeByFullName.TryGetValue(creatureActionTypeName, out Type creatureActionType) &&
            creature.TryGetComponent(creatureActionType, out Component component) && component is CreatureAction creatureAction)
        {
            actions[creature] = creatureAction;
        }
    }

    public static void AggressiveWhenSeeTargetChanged(NitroxId creatureId, NitroxId targetId, bool locked, float aggressionAmount)
    {
        if (!NitroxEntity.TryGetComponentFrom(creatureId, out AggressiveWhenSeeTarget aggressiveWhenSeeTarget) ||
            !NitroxEntity.TryGetObjectFrom(targetId, out GameObject targetObject) ||
            IsDead(aggressiveWhenSeeTarget))
        {
            return;
        }

        Creature creature = aggressiveWhenSeeTarget.creature;

        // Code from AggressiveWhenSeeTarget.ScanForAggressionTarget
        creature.Aggression.Value = aggressionAmount;
        LastTarget lastTarget = aggressiveWhenSeeTarget.lastTarget;
        lastTarget.SetTargetInternal(targetObject);
        lastTarget.targetLocked = locked;

        if (aggressiveWhenSeeTarget.sightedSound && !aggressiveWhenSeeTarget.sightedSound.GetIsPlaying())
        {
            // This call doesn't broadcast a sound packet
            aggressiveWhenSeeTarget.sightedSound.StartEvent();
        }

        if (creature.TryGetComponent(out AttackLastTarget attackLastTarget))
        {
            attackLastTarget.currentTarget = targetObject;
        }
    }

    public static void AttackCyclopsTargetChanged(NitroxId creatureId, NitroxId targetId, float aggressiveToNoiseAmount)
    {
        if (!NitroxEntity.TryGetComponentFrom(creatureId, out AttackCyclops attackCyclops) ||
            !NitroxEntity.TryGetObjectFrom(targetId, out GameObject targetObject) ||
            IsDead(attackCyclops))
        {
            return;
        }

        // Kinda stuff from AttackCyclops.UpdateAggression
        attackCyclops.aggressiveToNoise.Value = aggressiveToNoiseAmount;
        // Force currentTarget to null to ensure SetCurrentTarget detects a change
        attackCyclops.currentTarget = null;
        attackCyclops.SetCurrentTarget(targetObject, targetObject.GetComponent<CyclopsDecoy>());
    }

    public static void RangedAttackLastTargetUpdate(NitroxId creatureId, NitroxId targetId, int attackTypeIndex, ActionState state)
    {
        if (!NitroxEntity.TryGetComponentFrom(creatureId, out RangedAttackLastTarget rangedAttackLastTarget) ||
            !NitroxEntity.TryGetObjectFrom(targetId, out GameObject targetObject) ||
            IsDead(rangedAttackLastTarget))
        {
            return;
        }

        RangedAttackLastTarget.RangedAttackType attackType = rangedAttackLastTarget.attackTypes[attackTypeIndex];
        rangedAttackLastTarget.currentAttack = attackType;
        rangedAttackLastTarget.currentTarget = targetObject;

        switch (state)
        {
            case ActionState.CHARGING:
                rangedAttackLastTarget.StartCharging(attackType);
                break;
            case ActionState.CASTING:
                rangedAttackLastTarget.StartCasting(attackType);
                break;
        }
    }

    public static void CreaturePoopPerformed(NitroxId creatureId)
    {
        if (NitroxEntity.TryGetComponentFrom(creatureId, out Poop poop))
        {
            // Code from Poop.Perform
            SafeAnimator.SetBool(poop.creature.GetAnimator(), poop.animationParameterName, false);
            poop.recourceSpawned = true;
        }
    }

    public bool TryGetActionForCreature(Creature creature, out CreatureAction action)
    {
        if (!actions.TryGetValue(creature, out action))
        {
            return false;
        }

        // The simulating player can send actions before learning that the creature died, they'd make its corpse act as if it was alive
        if (IsDead(creature))
        {
            actions.Remove(creature);
            action = null;
            return false;
        }
        return true;
    }

    public bool IsCreatureActionWhitelisted(CreatureAction creatureAction)
    {
        return creatureActionWhitelist.Contains(creatureAction.GetType());
    }

    public bool IsCreatureWhitelisted(Creature creature)
    {
        return syncedCreatureWhitelist.Contains(creature.GetType());
    }

    /// <summary>
    ///     True if the bitten player's own copy of this creature does the biting, which its synced target and actions
    ///     make it do. False for the creatures whose bites the simulating player reports.
    /// </summary>
    public bool IsAttackReplayedByVictim(Creature creature)
    {
        return IsAttackReplayedByVictim(creature.GetType());
    }

    internal bool IsAttackReplayedByVictim(Type creatureType)
    {
        return syncedCreatureWhitelist.Contains(creatureType) && !reportedBiteCreatures.Contains(creatureType);
    }

    /// <summary>
    /// Remote AI updates can still arrive for a creature which already died for us (sent by its simulating player before it learned about the death)
    /// </summary>
    private static bool IsDead(Component creatureComponent)
    {
        LiveMixin liveMixin = creatureComponent.GetComponentInParent<LiveMixin>();
        return liveMixin && !liveMixin.IsAlive();
    }
}
