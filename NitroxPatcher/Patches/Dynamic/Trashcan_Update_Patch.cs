using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Syncs item deletion in trash can (under certain simulation ownership conditions, see <see cref="BroadcastDeletion"/>)
/// </summary>
public sealed partial class Trashcan_Update_Patch : NitroxPatch, IDynamicPatch
{
    internal static readonly MethodInfo TARGET_METHOD = Reflect.Method((Trashcan t) => t.Update());

    /*
     * if (this.storageContainer.container.RemoveItem(item, true))
     * {
     *     BroadcastDeletion(this, item.gameObject);      <------- [INSERTED LINE]
     *     UnityEngine.Object.Destroy(item.gameObject);
     * }
     */
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions).End()
                                            .Insert([
                                                new CodeInstruction(OpCodes.Ldarg_0),
                                                new CodeInstruction(OpCodes.Ldloc_1),
                                                new CodeInstruction(OpCodes.Callvirt, Reflect.Property((Component t) => t.gameObject).GetGetMethod()),
                                                new CodeInstruction(OpCodes.Call, Reflect.Method(() => BroadcastDeletion(default, default)))
                                            ])
                                            .InstructionEnumeration();
    }

    /// <summary>
    ///     See <see cref="Trashcans" /> for which game reports the deletion.
    /// </summary>
    public static void BroadcastDeletion(Trashcan trashcan, GameObject gameObject)
    {
        Resolve<Trashcans>().Destroyed(trashcan, gameObject);
    }
}
