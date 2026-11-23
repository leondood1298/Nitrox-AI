using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     What a creature's bite does to the player it bites beyond the damage, which only the bitten player's game can do. The
///     simulating player's game decides the bite on another player's body, where the game's own checks for a
///     <see cref="Player" /> do not apply (see <see cref="CreatureAttacksOnPlayers" />); the bitten player's game runs them
///     when the bite arrives. Unity-bound.
/// </summary>
public sealed class CreatureAttackEffects
{
    /// <summary>
    ///     Runs the creature's own effects of a bite on the local player, as its MeleeAttack.OnTouch does for a
    ///     <see cref="Player" />.
    /// </summary>
    /// <returns>Damage the bite adds to what was reported.</returns>
    public float ApplyOnLocalPlayer(GameObject creatureObject, float originalDamage, DamageType type)
    {
        Player player = Player.main;
        if (!creatureObject || !player || player.cinematicModeActive)
        {
            return 0f;
        }

        CrabsnakeMeleeAttack crabsnakeAttack = creatureObject.GetComponentInChildren<CrabsnakeMeleeAttack>();
        CrabSnake crabSnake = creatureObject.GetComponent<CrabSnake>();
        if (crabsnakeAttack && crabSnake)
        {
            return GrabByCrabsnake(player, crabsnakeAttack, crabSnake, originalDamage);
        }

        ReaperMeleeAttack reaperAttack = creatureObject.GetComponentInChildren<ReaperMeleeAttack>();
        if (reaperAttack)
        {
            KillByReaper(player, reaperAttack, originalDamage, type);
        }
        return 0f;
    }

    /// <summary>
    ///     CrabsnakeMeleeAttack.OnTouch: unless coiled up or in an aquarium, a Crabsnake grabs the player in a cinematic
    ///     that hurts more.
    /// </summary>
    private static float GrabByCrabsnake(Player player, CrabsnakeMeleeAttack attack, CrabSnake crabSnake, float originalDamage)
    {
        if (crabSnake.state == CrabSnake.State.Coil || crabSnake.IsWaterParkCreature())
        {
            return 0f;
        }
        float damage = DamageSystem.CalculateDamage(originalDamage + attack.cinematicAttackAdditionalDamage, DamageType.Normal, player.gameObject, null);
        PlayerCinematicController cinematic = player.liveMixin.health - damage > 0f ? attack.playerAttackCinematicController : attack.playerKillCinematicController;
        if (cinematic)
        {
            cinematic.StartCinematicMode(player);
            crabSnake.cinematicMode = true;
        }
        return attack.cinematicAttackAdditionalDamage;
    }

    /// <summary>
    ///     ReaperMeleeAttack.OnTouch: a bite that kills the player plays the Reaper's death cinematic.
    /// </summary>
    private static void KillByReaper(Player player, ReaperMeleeAttack attack, float originalDamage, DamageType type)
    {
        float damage = DamageSystem.CalculateDamage(originalDamage, type, player.gameObject, null);
        if (player.liveMixin.health - damage > 0f || !attack.playerDeathCinematic)
        {
            return;
        }
        attack.playerDeathCinematic.StartCinematicMode(player);
        if (attack.playerAttackSound)
        {
            attack.playerAttackSound.Play();
        }
        attack.reaper.OnGrabPlayer();
    }
}
