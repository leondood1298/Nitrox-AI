using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic.PlayerLogic;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Makes creature attacks hurt players who are not simulating the creature. A creature only sees targets, gets
///     aggressive and attacks on the game of the player simulating it, where every other player is a body that takes no
///     damage. That game therefore reports each hit on such a body to the player it belongs to, who takes the damage on
///     their own game.
/// </summary>
/// <remarks>
///     This holds for every creature: the bitten player's own copy never bites (<see cref="CreatureBites" />). What only
///     the bitten player's game can do, a grab cinematic for example, it does when the hit arrives (<see cref="CreatureAttackEffects" />).
/// </remarks>
public sealed class CreatureAttacksOnPlayers(IPacketSender packetSender, SimulationOwnership simulationOwnership, CreatureAttackEffects attackEffects)
{
    private readonly IPacketSender packetSender = packetSender;
    private readonly SimulationOwnership simulationOwnership = simulationOwnership;
    private readonly CreatureAttackEffects attackEffects = attackEffects;

    private int reportedAttacksBeingApplied;

    /// <summary>
    ///     Whether a hit seen by this game has to be reported to the player who was hit.
    /// </summary>
    public static bool ShouldReport(bool victimIsRemotePlayer, bool localPlayerSimulatesCreature)
    {
        return victimIsRemotePlayer && localPlayerSimulatesCreature;
    }

    /// <summary>
    ///     Whether a hit on the local player by this game's copy of a creature must be skipped because the player
    ///     simulating the creature reports its hits. Without this, a copy that happens to be aggressive here (it was just
    ///     hit, for example) would bite a second time.
    /// </summary>
    /// <param name="movedByAnotherPlayer">
    ///     False for a creature nobody simulates: nobody would report its hits, so the local copy keeps biting as before.
    /// </param>
    public static bool ShouldLeaveToSimulatingPlayer(bool victimIsLocalPlayer, bool localPlayerSimulatesCreature, bool movedByAnotherPlayer, bool applyingReportedAttack)
    {
        return victimIsLocalPlayer && !localPlayerSimulatesCreature && movedByAnotherPlayer && !applyingReportedAttack;
    }

    /// <summary>
    ///     Whether this game's copy of a creature may bite another player's body. Only when the bite will be reported
    ///     (see <see cref="ShouldReport" />) and when the game would let the creature bite that player on their own game.
    /// </summary>
    public bool CanBiteRemotePlayer(Creature creature, RemotePlayer remotePlayer)
    {
        if (!creature || !creature.TryGetNitroxId(out NitroxId creatureId))
        {
            return false;
        }
        if (!ShouldReport(true, simulationOwnership.HasAnyLockType(creatureId)))
        {
            return false;
        }

        // In a vehicle the game lets the vehicle take the bite
        return remotePlayer.LiveMixin && remotePlayer.LiveMixin.IsAlive() && remotePlayer.CanBeAttacked() && !remotePlayer.Vehicle;
    }

    /// <summary>
    ///     To be called for damage dealt on this game. <paramref name="dealer" /> is what the game passed to
    ///     <see cref="LiveMixin.TakeDamage" />; <see cref="MeleeAttack.OnTouch" /> passes the creature.
    /// </summary>
    public void ReportIfHitOnRemotePlayer(LiveMixin victim, GameObject dealer, float originalDamage, DamageType type)
    {
        if (!victim.TryGetComponent(out RemotePlayerIdentifier remotePlayerIdentifier) || !TryGetCreature(dealer, out Creature creature, out NitroxId creatureId))
        {
            return;
        }

        if (ShouldReport(true, simulationOwnership.HasAnyLockType(creatureId)))
        {
            packetSender.Send(new CreatureAttackedPlayer(creatureId, remotePlayerIdentifier.RemotePlayer.SessionId, originalDamage, type));
        }
    }

    /// <summary>
    ///     Called before ShockerMeleeAttack.OnTouch on the game of the Ampeel's simulating player. The game shocks whatever
    ///     has a <see cref="LiveMixin" /> without naming the Ampeel, so a shock on another player's body hurt nobody; it is
    ///     reported to that player instead, with the checks the game makes for its own player.
    /// </summary>
    /// <returns>True if the touch was a shock on another player, which the game must then not handle.</returns>
    public bool TryShockRemotePlayer(ShockerMeleeAttack attack, Collider collider) => TryShockRemotePlayer(attack, collider ? attack.GetTarget(collider) : null);

    private bool TryShockRemotePlayer(ShockerMeleeAttack attack, GameObject target)
    {
        Creature creature = attack.creature;
        if (!target || !target.TryGetComponent(out RemotePlayerIdentifier remotePlayerIdentifier) || !CanBiteRemotePlayer(creature, remotePlayerIdentifier.RemotePlayer) ||
            !creature.TryGetNitroxId(out NitroxId creatureId))
        {
            return false;
        }
        if (attack.frozen || Time.time < attack.timeLastElectricalDamage + attack.electricalDamageInterval || !attack.liveMixin.IsAlive())
        {
            return true;
        }

        attack.timeLastElectricalDamage = Time.time;
        packetSender.Send(new CreatureAttackedPlayer(creatureId, remotePlayerIdentifier.RemotePlayer.SessionId, attack.electricalDamage, DamageType.Electrical));
        packetSender.Send(new CreatureBitePlayed(creatureId));
        if (attack.electricalAttackSound)
        {
            Utils.PlayEnvSound(attack.electricalAttackSound, target.transform.position, 20f);
        }
        return true;
    }

    /// <summary>
    ///     To be called before damage is applied on this game. True if it must not be applied.
    /// </summary>
    public bool IsLeftToSimulatingPlayer(LiveMixin victim, GameObject dealer)
    {
        Player localPlayer = Player.main;
        if (!localPlayer || victim != localPlayer.liveMixin || !TryGetCreature(dealer, out Creature creature, out NitroxId creatureId))
        {
            return false;
        }

        return ShouldLeaveToSimulatingPlayer(true, simulationOwnership.HasAnyLockType(creatureId), creature.GetComponent<RemotelyControlled>(),
                                             reportedAttacksBeingApplied > 0);
    }

    public void ApplyReported(CreatureAttackedPlayer attack)
    {
        Player localPlayer = Player.main;
        if (!localPlayer || !localPlayer.liveMixin || !localPlayer.liveMixin.IsAlive())
        {
            return;
        }

        // What the game checks before a creature may hurt its own player (MeleeAttack.CanDealDamageTo). In a vehicle the
        // game lets the vehicle take the bite; the other player's game cannot tell, as it only sees a body.
        if (!localPlayer.CanBeAttacked() || localPlayer.GetVehicle())
        {
            return;
        }

        NitroxEntity.TryGetObjectFrom(attack.CreatureId, out GameObject creatureObject);

        reportedAttacksBeingApplied++;
        try
        {
            // What the creature's game cannot do on another player's body: a grab cinematic, for example
            float extraDamage = attackEffects.ApplyOnLocalPlayer(creatureObject, attack.OriginalDamage, attack.Type);
            // Same arguments as MeleeAttack.OnTouch gives; the creature may not be loaded on this game.
            localPlayer.liveMixin.TakeDamage(attack.OriginalDamage + extraDamage, default, attack.Type, creatureObject);
        }
        finally
        {
            reportedAttacksBeingApplied--;
        }
    }

    private static bool TryGetCreature(GameObject dealer, out Creature creature, out NitroxId creatureId)
    {
        creatureId = null;
        creature = dealer ? dealer.GetComponentInParent<Creature>() : null;
        return creature && creature.TryGetNitroxId(out creatureId);
    }
}
