using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Creature.ScanCreatureActions stops the current action when it was disabled (a Cuddlefish told to stay, an aquarium
///     predator activated). Stopping it goes through the same rule as in Creature.UpdateBehaviour (<see cref="AI.MayRunAction" />).
/// </summary>
public sealed partial class Creature_ScanCreatureActions_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Creature t) => t.ScanCreatureActions());

    /*
     * this.prevBestAction.StopPerform(this, Time.time);   =>   Creature_ScanCreatureActions_Patch.StopPerform(this.prevBestAction, this, Time.time);
     */
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions).MatchStartForward(new CodeMatch(OpCodes.Callvirt, Reflect.Method((CreatureAction t) => t.StopPerform(default, default))))
                                            .SetInstruction(new CodeInstruction(OpCodes.Call, Reflect.Method(() => StopPerform(default, default, default))))
                                            .InstructionEnumeration();
    }

    public static void StopPerform(CreatureAction action, Creature creature, float time)
    {
        if (Resolve<AI>().MayRunAction(creature, action, false))
        {
            action.StopPerform(creature, time);
        }
    }
}
