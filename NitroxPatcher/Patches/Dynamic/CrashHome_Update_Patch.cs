using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class CrashHome_Update_Patch : NitroxPatch, IDynamicPatch
{
    internal static readonly MethodInfo TARGET_METHOD = Reflect.Method((CrashHome t) => t.Update());

    /*
     * if (!this.crash && this.spawnTime < 0f)
     * {
     *     this.spawnTime = (float)(main.timePassed + 1200.0);  [REMOVED LINE]
     *     PairWithItsCrash(this);                              [INSERTED LINE]
     * }
     */
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions).MatchEndForward([
                                                new CodeMatch(OpCodes.Ldfld),
                                                new CodeMatch(OpCodes.Ldc_R4),
                                                new CodeMatch(OpCodes.Bge_Un)
                                            ])
                                            .Advance(1)
                                            .RemoveInstructions(7)
                                            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                                            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, Reflect.Method(() => PairWithItsCrash(default))))
                                            .InstructionEnumeration();
    }

    /// <remarks>
    ///     A home without its crashfish here does not start a new one: the fish is a stored entity which may just not be
    ///     loaded yet. The server says when a home makes a new one, once its fish is gone (CrashHomeRespawns).
    /// </remarks>
    public static void PairWithItsCrash(CrashHome crashHome)
    {
        Resolve<CrashHomes>().TryPair(crashHome);
    }
}
