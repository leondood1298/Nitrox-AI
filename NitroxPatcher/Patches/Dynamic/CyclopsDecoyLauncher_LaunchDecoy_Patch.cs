using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Hands the decoy the launcher just made to <see cref="CyclopsDecoys" />, which shows it to the others.
/// </summary>
public sealed partial class CyclopsDecoyLauncher_LaunchDecoy_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((CyclopsDecoyLauncher t) => t.LaunchDecoy());

    /*
     * CyclopsDecoy component = Object.Instantiate(this.decoyPrefab, ...).GetComponent<CyclopsDecoy>();
     * CyclopsDecoyLauncher_LaunchDecoy_Patch.Launched(this, component); <--- INSERTED LINE
     * if (component) { component.launch = true; }
     */
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions).MatchEndForward([
                                                new CodeMatch(OpCodes.Callvirt, Reflect.Method((GameObject t) => t.GetComponent<CyclopsDecoy>())),
                                                new CodeMatch(OpCodes.Stloc_0)
                                            ])
                                            .Advance(1)
                                            .InsertAndAdvance([
                                                new CodeInstruction(OpCodes.Ldarg_0),
                                                new CodeInstruction(OpCodes.Ldloc_0),
                                                new CodeInstruction(OpCodes.Call, Reflect.Method(() => Launched(default, default)))
                                            ])
                                            .InstructionEnumeration();
    }

    public static void Launched(CyclopsDecoyLauncher launcher, CyclopsDecoy decoy)
    {
        Resolve<CyclopsDecoys>().OnLaunched(launcher, decoy);
    }
}
