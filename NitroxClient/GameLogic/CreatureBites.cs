using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Every creature's bites are decided by the game of the player simulating it and shown once on every game, wherever
///     they land. The simulating game reports a bite on another player to that player (<see cref="CreatureAttacksOnPlayers" />)
///     and sends each bite (<see cref="CreatureBitePlayed" />); the copies on the other games show it and never bite on
///     their own. Before, the bitten player's own copy of a Reaper bit them where it touched them there, which need not
///     be where the simulating player's game had it, and a third player saw no bite at all.
/// </summary>
public sealed class CreatureBites(IPacketSender packetSender, SimulationOwnership simulationOwnership)
{
    /// <summary>
    ///     Same as MeleeAttack.OnTouch gives Utils.PlayEnvSound.
    /// </summary>
    private const float BITE_SOUND_DISTANCE = 20f;

    private readonly IPacketSender packetSender = packetSender;
    private readonly SimulationOwnership simulationOwnership = simulationOwnership;

    /// <summary>
    ///     Whether this game's copy of a creature may bite on its own.
    /// </summary>
    /// <param name="movedByAnotherPlayer">False for a creature nobody simulates: nobody would send its bites.</param>
    public static bool MayBiteHere(bool localPlayerSimulatesCreature, bool movedByAnotherPlayer)
    {
        return localPlayerSimulatesCreature || !movedByAnotherPlayer;
    }

    /// <summary>
    ///     Called before MeleeAttack.CanBite, and before the attacks of a creature that bites in its own way.
    /// </summary>
    public bool MayBite(Creature creature)
    {
        if (!creature || !creature.TryGetNitroxId(out NitroxId creatureId))
        {
            return true;
        }
        return MayBiteHere(simulationOwnership.HasAnyLockType(creatureId), creature.GetComponent<RemotelyControlled>());
    }

    public bool MayBite(MeleeAttack meleeAttack) => MayBite(meleeAttack.creature);

    /// <summary>
    ///     Called when a creature of this game bites.
    /// </summary>
    public void Bit(Creature creature)
    {
        if (creature && creature.TryGetNitroxId(out NitroxId creatureId) && simulationOwnership.HasAnyLockType(creatureId))
        {
            packetSender.Send(new CreatureBitePlayed(creatureId));
        }
    }

    public void Bit(MeleeAttack meleeAttack) => Bit(meleeAttack.creature);

    /// <summary>
    ///     What MeleeAttack.OnTouch shows of a bite: the biting pose (MeleeAttack.ManagedUpdate, from timeLastBite), the
    ///     blood and the bite sound. An Ampeel's shock shows its own sound.
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
        if (meleeAttack is ShockerMeleeAttack shockerMeleeAttack && shockerMeleeAttack.electricalAttackSound)
        {
            Utils.PlayEnvSound(shockerMeleeAttack.electricalAttackSound, mouthPosition, BITE_SOUND_DISTANCE);
            return;
        }
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
