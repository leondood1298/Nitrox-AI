using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The game makes 5% of the peepers that appear outside the Safe Shallows heroes from birth with Random.value, so every
///     player and every load got its own hero peepers. The roll is now fixed per peeper and only made by its simulating player.
/// </summary>
public sealed partial class Peeper_InitializeOnce_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Peeper t) => t.InitializeOnce());

    /*
     * if (!string.Equals("safeShallows", ...) && Peeper_InitializeOnce_Patch.GetBornHeroRoll(this) < 0.05f)    <--- was Random.value
     * {
     *     timeRechargeEnzyme = 1f;
     *     BecomeHero();
     * }
     */
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions).MatchStartForward(new CodeMatch(OpCodes.Call, AccessTools.PropertyGetter(typeof(UnityEngine.Random), nameof(UnityEngine.Random.value))))
                                            .ThrowIfInvalid("Could not find the born hero roll")
                                            .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                                            .Insert(new CodeInstruction(OpCodes.Call, Reflect.Method(() => GetBornHeroRoll(default))))
                                            .InstructionEnumeration();
    }

    public static float GetBornHeroRoll(Peeper peeper)
    {
        return Resolve<CreatureInfections>().GetBornHeroRoll(peeper);
    }
}
