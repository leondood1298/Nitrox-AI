using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A creature bites, which the other games show if it is a Stalker or Sand Shark, see <see cref="CreatureBites" />.
/// </summary>
public sealed partial class MeleeAttack_OnTouch_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MeleeAttack t) => t.OnTouch(default(Collider)));

    /*
     * if (this.CanBite(target))
     * {
     *     this.timeLastBite = Time.time;
     *     MeleeAttack_OnTouch_Patch.Bit(this);   <--- [INSERTED LINE]
     */
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions).MatchEndForward(new CodeMatch(OpCodes.Stfld, Reflect.Field((MeleeAttack t) => t.timeLastBite)))
                                            .Advance(1)
                                            .Insert(new CodeInstruction(OpCodes.Ldarg_0),
                                                    new CodeInstruction(OpCodes.Call, Reflect.Method(() => Bit(default))))
                                            .InstructionEnumeration();
    }

    public static void Bit(MeleeAttack meleeAttack) => Resolve<CreatureBites>().Bit(meleeAttack);
}
