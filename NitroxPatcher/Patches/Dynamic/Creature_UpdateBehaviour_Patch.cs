using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Decides, where the game starts, stops and performs a creature's action, whether this game may run it (see
///     <see cref="AI.MayRunAction" />). Every action of the game overrides these three methods without calling the base, so
///     patching <see cref="CreatureAction" /> itself never stopped a copy of another player's creature from running them.
/// </summary>
public sealed partial class Creature_UpdateBehaviour_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Creature t) => t.UpdateBehaviour(default, default));

    /*
     * prevBestAction.StopPerform(this, time);          =>  Creature_UpdateBehaviour_Patch.StopPerform(prevBestAction, this, time);
     * bestAction.StartPerform(this, time);             =>  Creature_UpdateBehaviour_Patch.StartPerform(bestAction, this, time);
     * bestAction.Perform(this, time, deltaTime);       =>  Creature_UpdateBehaviour_Patch.Perform(bestAction, this, time, deltaTime);
     */
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo stopPerform = Reflect.Method((CreatureAction t) => t.StopPerform(default, default));
        MethodInfo startPerform = Reflect.Method((CreatureAction t) => t.StartPerform(default, default));
        MethodInfo perform = Reflect.Method((CreatureAction t) => t.Perform(default, default, default));

        return new CodeMatcher(instructions).MatchStartForward(new CodeMatch(OpCodes.Callvirt, stopPerform))
                                            .SetInstruction(new CodeInstruction(OpCodes.Call, Reflect.Method(() => StopPerform(default, default, default))))
                                            .MatchStartForward(new CodeMatch(OpCodes.Callvirt, startPerform))
                                            .SetInstruction(new CodeInstruction(OpCodes.Call, Reflect.Method(() => StartPerform(default, default, default))))
                                            .MatchStartForward(new CodeMatch(OpCodes.Callvirt, perform))
                                            .SetInstruction(new CodeInstruction(OpCodes.Call, Reflect.Method(() => Perform(default, default, default, default))))
                                            .InstructionEnumeration();
    }

    public static void StopPerform(CreatureAction action, Creature creature, float time)
    {
        if (Resolve<AI>().MayRunAction(creature, action, false))
        {
            action.StopPerform(creature, time);
        }
    }

    public static void StartPerform(CreatureAction action, Creature creature, float time)
    {
        if (Resolve<AI>().MayRunAction(creature, action, false))
        {
            action.StartPerform(creature, time);
        }
    }

    public static void Perform(CreatureAction action, Creature creature, float time, float deltaTime)
    {
        if (Resolve<AI>().MayRunAction(creature, action, true))
        {
            action.Perform(creature, time, deltaTime);
        }
    }
}
