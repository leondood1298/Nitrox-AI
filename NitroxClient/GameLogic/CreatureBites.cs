using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Shows the bites of Stalkers and Sand Sharks once on every game, wherever they land. Their bites are decided by the
///     simulating player's game, which reports a bite on another player to that player (<see cref="CreatureAttacksOnPlayers" />).
///     Every other game's copy bit only what touched it there, so a third player never saw a bite on another player, and
///     the bitten player saw their own copy's bite, which need not be the one that hurt them. Now the simulating game
///     sends each bite (<see cref="CreatureBitePlayed" />) and the other copies show it instead of biting on their own.
/// </summary>
/// <remarks>
///     Other creatures whose bites are reported (those AI does not sync) still bite only on the simulating game: their
///     copies elsewhere are not aggressive, so they show no bite at all.
/// </remarks>
public sealed class CreatureBites(IPacketSender packetSender, SimulationOwnership simulationOwnership, AI ai)
{
    /// <summary>
    ///     Same as MeleeAttack.OnTouch gives Utils.PlayEnvSound.
    /// </summary>
    private const float BITE_SOUND_DISTANCE = 20f;

    private readonly IPacketSender packetSender = packetSender;
    private readonly SimulationOwnership simulationOwnership = simulationOwnership;
    private readonly AI ai = ai;

    /// <summary>
    ///     Whether this game's copy of a creature may bite on its own.
    /// </summary>
    /// <param name="movedByAnotherPlayer">False for a creature nobody simulates: nobody would send its bites.</param>
    public static bool MayBiteHere(bool bitesShownFromSimulatingPlayer, bool localPlayerSimulatesCreature, bool movedByAnotherPlayer)
    {
        return !bitesShownFromSimulatingPlayer || localPlayerSimulatesCreature || !movedByAnotherPlayer;
    }

    /// <summary>
    ///     Whether a bite of this game's copy is sent to the other games.
    /// </summary>
    public static bool ShouldSend(bool bitesShownFromSimulatingPlayer, bool localPlayerSimulatesCreature)
    {
        return bitesShownFromSimulatingPlayer && localPlayerSimulatesCreature;
    }

    /// <summary>
    ///     Called before MeleeAttack.CanBite.
    /// </summary>
    public bool MayBite(MeleeAttack meleeAttack)
    {
        Creature creature = meleeAttack.creature;
        if (!creature || !creature.TryGetNitroxId(out NitroxId creatureId))
        {
            return true;
        }
        return MayBiteHere(ai.AreBitesShownFromSimulatingPlayer(creature), simulationOwnership.HasAnyLockType(creatureId), creature.GetComponent<RemotelyControlled>());
    }

    /// <summary>
    ///     Called when MeleeAttack.OnTouch bites.
    /// </summary>
    public void Bit(MeleeAttack meleeAttack)
    {
        Creature creature = meleeAttack.creature;
        if (creature && creature.TryGetNitroxId(out NitroxId creatureId) &&
            ShouldSend(ai.AreBitesShownFromSimulatingPlayer(creature), simulationOwnership.HasAnyLockType(creatureId)))
        {
            packetSender.Send(new CreatureBitePlayed(creatureId));
        }
    }

    /// <summary>
    ///     What MeleeAttack.OnTouch shows of a bite: the biting pose (MeleeAttack.ManagedUpdate, from timeLastBite), the
    ///     blood and the bite sound.
    /// </summary>
    public void Apply(CreatureBitePlayed bite)
    {
        if (simulationOwnership.HasAnyLockType(bite.CreatureId) || !NitroxEntity.TryGetComponentFrom(bite.CreatureId, out Creature creature))
        {
            return;
        }
        MeleeAttack meleeAttack = creature.GetComponentInChildren<MeleeAttack>();
        if (!meleeAttack)
        {
            return;
        }
        LiveMixin liveMixin = creature.GetComponent<LiveMixin>();
        if (liveMixin && !liveMixin.IsAlive())
        {
            return;
        }

        meleeAttack.timeLastBite = Time.time;
        Vector3 mouthPosition = meleeAttack.mouth ? meleeAttack.mouth.transform.position : creature.transform.position;
        if (meleeAttack.damageFX)
        {
            Object.Instantiate(meleeAttack.damageFX, mouthPosition, meleeAttack.damageFX.transform.rotation);
        }
        if (meleeAttack.attackSound)
        {
            Utils.PlayEnvSound(meleeAttack.attackSound, mouthPosition, BITE_SOUND_DISTANCE);
        }
    }
}
