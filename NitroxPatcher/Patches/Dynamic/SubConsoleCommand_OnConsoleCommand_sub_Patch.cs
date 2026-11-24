using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Nitrox.Model.DataStructures.GameLogic;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Prevents local player from using "sub" command without the rank the console needs (<see cref="ConsoleCommandPolicy.REQUIRED_PERMS" />).
/// Once they have the permissions, sync this command.
/// </summary>
public sealed partial class SubConsoleCommand_OnConsoleCommand_sub_Patch : NitroxPatch, IDynamicPatch
{
    internal static readonly MethodInfo TARGET_METHOD = Reflect.Method((SubConsoleCommand t) => t.OnConsoleCommand_sub(default));

    public static bool Prefix(NotificationCenter.Notification n)
    {
        if (!ConsoleCommandPolicy.MaySubmit(Resolve<LocalPlayer>().Permissions))
        {
            Log.InGame(Language.main.Get("Nitrox_MissingPermission").Replace("{PERMISSION}", ConsoleCommandPolicy.REQUIRED_PERMS.ToString()));
            return false;
        }

        string text = (string)n.data[0];
        if (!string.IsNullOrEmpty(text) && !text.ToLowerInvariant().Equals("cyclops"))
        {
            Log.InGame(Language.main.Get("Nitrox_CommandNotAvailable"));
            return false;
        }
        return true;
    }

    /*
     * REPLACE:
     * LightmappedPrefabs.main.RequestScenePrefab(text, new LightmappedPrefabs.OnPrefabLoaded(this.OnSubPrefabLoaded));
     * BY:
     * LightmappedPrefabs.main.RequestScenePrefab(text, new LightmappedPrefabs.OnPrefabLoaded(SubConsoleCommand_OnConsoleCommand_sub_Patch.WrappedCallback));
     */
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions).MatchEndForward([
                                                new CodeMatch(OpCodes.Stfld),
                                                new CodeMatch(OpCodes.Ldsfld),
                                                new CodeMatch(OpCodes.Ldloc_0),
                                                new CodeMatch(OpCodes.Ldarg_0)
                                            ])
                                            .SetOpcodeAndAdvance(OpCodes.Ldnull)
                                            .SetOperandAndAdvance(Reflect.Method(() => WrappedCallback(default)))
                                            .InstructionEnumeration();
    }

    public static void WrappedCallback(GameObject prefab)
    {
        SubConsoleCommand instance = SubConsoleCommand.main;
        // Call the original callback and then get the object it created to broadcast its creation
        instance.OnSubPrefabLoaded(prefab);
        Resolve<NitroxConsole>().Spawn(instance.lastCreatedSub);
    }
}
