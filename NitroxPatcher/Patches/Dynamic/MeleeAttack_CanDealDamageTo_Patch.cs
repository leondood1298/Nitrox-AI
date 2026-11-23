using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.PlayerLogic;
using NitroxClient.MonoBehaviours.Cyclops;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// For some reason, when a creature attacks a Cyclops, its main rigidbody is sometimes not properly detected by the raycasts.
/// Thus we change the target check (when it's a Cyclops) to also check if the collider is part of the target cyclops.
/// <br />
/// Also lets a creature bite the body of another player. The game only accepts a <see cref="Player" />, a creature or a
/// vehicle as something to bite, so a creature could chase another player's body but never hurt it
/// (see <see cref="CreatureAttacksOnPlayers" />).
/// </summary>
public sealed partial class MeleeAttack_CanDealDamageTo_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MeleeAttack t) => t.CanDealDamageTo(default));

    /*
     * REPLACE:
     * if (!(gameObject == target) && !(gameObject == base.gameObject) && !(gameObject.GetComponent<Creature>() != null))
     * 
     * BY:
     * if (!(gameObject == target) && !(gameObject == base.gameObject) && !(gameObject.GetComponent<Creature>() != null) && !MeleeAttack_CanDealDamageTo_Patch.IsValidTarget(target, gameObject))
     */
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        // Match the latest part of the if
        CodeMatcher matcher = new CodeMatcher(instructions).MatchEndForward([
            new CodeMatch(OpCodes.Ldloc_S),
            new CodeMatch(OpCodes.Callvirt, Reflect.Method((GameObject t) => t.GetComponent<Creature>())),
            new CodeMatch(OpCodes.Ldnull),
            new CodeMatch(OpCodes.Call),
            new CodeMatch(OpCodes.Brtrue),
        ]);

        // We copy the "brtrue" so we can easily insert another clause in the "if"
        CodeInstruction brTrueInstruction = matcher.Instruction;

        // Advance 1 to insert after the matched pattern
        matcher.Advance(1)
               .Insert([
                   new CodeInstruction(OpCodes.Ldarg_1), // target
                   new CodeInstruction(OpCodes.Ldloc_S, (byte)7), // gameObject
                   new CodeInstruction(OpCodes.Call, Reflect.Method(() => IsValidTarget(default, default))),
                   brTrueInstruction
                ]);
        
        /*
         * REPLACE:
         * (!this.canBitePlayer || player == null)
         *
         * BY:
         * (!this.canBitePlayer || MeleeAttack_CanDealDamageTo_Patch.GetPlayerOrBitableRemotePlayer(player, this, target) == null)
         */
        matcher.Start()
               .MatchEndForward([
                   new CodeMatch(OpCodes.Ldfld, Reflect.Field((MeleeAttack t) => t.canBitePlayer)),
                   new CodeMatch(OpCodes.Brfalse),
                   new CodeMatch(OpCodes.Ldloc_0)
               ])
               .Advance(1)
               .Insert([
                   new CodeInstruction(OpCodes.Ldarg_0), // this
                   new CodeInstruction(OpCodes.Ldarg_1), // target
                   new CodeInstruction(OpCodes.Call, Reflect.Method(() => GetPlayerOrBitableRemotePlayer(default, default, default)))
               ]);

        return matcher.InstructionEnumeration();
    }

    /// <summary>
    /// Stands in for the "player" of the game's check: the player if the target is one, otherwise the body of another
    /// player when this creature's bites on such bodies are reported to their player.
    /// </summary>
    public static Object GetPlayerOrBitableRemotePlayer(Player player, MeleeAttack meleeAttack, GameObject target)
    {
        if (player)
        {
            return player;
        }

        if (target.TryGetComponent(out RemotePlayerIdentifier remotePlayerIdentifier) &&
            Resolve<CreatureAttacksOnPlayers>().CanBiteRemotePlayer(meleeAttack.creature, remotePlayerIdentifier.RemotePlayer))
        {
            return remotePlayerIdentifier;
        }
        return null;
    }

    public static bool IsValidTarget(GameObject target, GameObject gameObject)
    {
        return target.GetComponent<NitroxCyclops>() && gameObject.GetComponentInParent<NitroxCyclops>(true);
    }
}
