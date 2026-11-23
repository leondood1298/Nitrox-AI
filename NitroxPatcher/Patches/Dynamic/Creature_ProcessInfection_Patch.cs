using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The game infects 5% of the creatures that appear outside the Safe Shallows with Random.value, so every player and
///     every load got its own answer. The roll is now fixed per creature and only made by its simulating player for a
///     creature whose infection nobody has decided yet.
/// </summary>
public sealed partial class Creature_ProcessInfection_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Creature t) => t.ProcessInfection());

    /*
     * if (!flag && !flag2 && infectedMixin && Creature_ProcessInfection_Patch.GetInfectionRoll(this) < 0.05f)    <--- was Random.value
     * {
     *     infectedMixin.SetInfectedAmount(1f);
     * }
     */
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions).MatchStartForward(new CodeMatch(OpCodes.Call, AccessTools.PropertyGetter(typeof(UnityEngine.Random), nameof(UnityEngine.Random.value))))
                                            .ThrowIfInvalid("Could not find the infection roll")
                                            .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                                            .Insert(new CodeInstruction(OpCodes.Call, Reflect.Method(() => GetInfectionRoll(default))))
                                            .InstructionEnumeration();
    }

    public static float GetInfectionRoll(Creature creature)
    {
        return Resolve<CreatureInfections>().GetInfectionRoll(creature);
    }
}
