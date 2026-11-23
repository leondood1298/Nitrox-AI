using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The launch button asks the server instead of launching: the ending starts only once the server has found every
///     player aboard, and then for all of them at once. The game's own conditions (rocket ready, gun disabled) stay in
///     front of the request.
/// </summary>
public sealed partial class LaunchRocket_OnHandClick_Patch : NitroxPatch, IDynamicPatch
{
    internal static readonly MethodInfo TARGET_METHOD = Reflect.Method((LaunchRocket t) => t.OnHandClick(default));

    /*
     * LaunchRocket.SetLaunchStarted();                              <--- [REPLACED BY THE NEXT TWO LINES]
     * LaunchRocket_OnHandClick_Patch.RequestRocketLaunch(this);     <--- [INSERTED LINE]
     * [Removed all lines after]
     */
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions).MatchStartForward([
                                                new CodeMatch(OpCodes.Call, Reflect.Method(() => LaunchRocket.SetLaunchStarted()))
                                            ])
                                            // Turned into "ldarg.0" in place, so the jumps of the gun check that land on it still do.
                                            // Not launched yet: a refused launch must leave the button working.
                                            .SetAndAdvance(OpCodes.Ldarg_0, null)
                                            .RemoveInstructions(10)
                                            .InsertAndAdvance([
                                                new CodeInstruction(OpCodes.Call, Reflect.Method(() => RequestRocketLaunch(default)))
                                            ]).InstructionEnumeration();
    }

    private static void RequestRocketLaunch(LaunchRocket launchRocket)
    {
        Resolve<Rockets>().RequestRocketLaunch(launchRocket);
    }
}
