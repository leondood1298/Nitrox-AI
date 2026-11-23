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
    private readonly SimulationOwnership simulationOwnership;
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
    ///     A creature's action is repeated this often even if it did not change, for players who loaded the creature
    ///     after it was last sent.
    /// </summary>
    private const float ACTION_RESEND_SECONDS = 5f;

    private readonly CreatureActionBroadcastFilter actionBroadcastFilter = new(ACTION_RESEND_SECONDS);

    public AI(IPacketSender packetSender, SimulationOwnership simulationOwnership)
    {
        this.packetSender = packetSender;
        this.simulationOwnership = simulationOwnership;
        Assembly assembly = Assembly.GetAssembly(typeof(CreatureAction));
        cachedCreatureActionTypeByFullName = assembly.GetTypes()
                                                     .Where(type => typeof(CreatureAction).IsAssignableFrom(type))
                                                     .ToDictionary(t => t.FullName, t => t);
    }

    /// <summary>
    ///     Tells the others which of the actions they replay (<see cref="IsReplayedAction" />) the creature does now, for
    ///     every creature: only those show on another game, so any other action, or none (ChooseBestAction returns nothing
    ///     while none of the creature's actions wants to run), is sent as none.
    /// </summary>
    public void BroadcastNewAction(NitroxId creatureId, Creature creature, CreatureAction newAction)
    {
        string actionTypeName = newAction && IsReplayedAction(newAction.GetType()) ? newAction.GetType().FullName : CreatureActionBroadcastFilter.NO_ACTION;
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
        // Only the actions this game replays are kept; any other one means the creature does none of them now
        if (cachedCreatureActionTypeByFullName.TryGetValue(creatureActionTypeName, out Type creatureActionType) && IsReplayedAction(creatureActionType) &&
            creature.TryGetComponent(creatureActionType, out Component component) && component is CreatureAction creatureAction)
        {
            actions[creature] = creatureAction;
        }
        else
        {
            actions.Remove(creature);
        }
    }

    /// <summary>
    ///     Called where Creature.UpdateBehaviour starts, stops or performs an action.
    /// </summary>
    public bool MayRunAction(Creature creature, CreatureAction action, bool isPerform)
    {
        bool hasId = creature.TryGetNitroxId(out NitroxId creatureId);
        return MayRunActionHere(hasId, hasId && simulationOwnership.HasAnyLockType(creatureId), IsReplayedAction(action.GetType()), isPerform);
    }

    /// <summary>
    ///     A creature that is not shared (it has no id) and one this player simulates run every action. A copy of a creature
    ///     another player simulates only starts and stops the actions it replays, which show the creature's pose; it never
    ///     performs one, which would steer it away from where its simulating player moves it, or attack on its own.
    /// </summary>
    public static bool MayRunActionHere(bool hasId, bool simulatedLocally, bool actionReplayed, bool isPerform)
    {
        return !hasId || simulatedLocally || (actionReplayed && !isPerform);
    }

    internal bool IsReplayedAction(Type actionType)
    {
        return creatureActionWhitelist.Contains(actionType);
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

    /// <summary>
    /// Remote AI updates can still arrive for a creature which already died for us (sent by its simulating player before it learned about the death)
    /// </summary>
    private static bool IsDead(Component creatureComponent)
    {
        LiveMixin liveMixin = creatureComponent.GetComponentInParent<LiveMixin>();
        return liveMixin && !liveMixin.IsAlive();
    }
}
