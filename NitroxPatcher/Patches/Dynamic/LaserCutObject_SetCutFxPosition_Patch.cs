using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The cutting effect follows the cut and points the local player's arms at it. When another player cuts, the effect is
///     shown here as well, and the local player's arms must stay where they are.
/// </summary>
public sealed partial class LaserCutObject_SetCutFxPosition_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((LaserCutObject t) => t.SetCutFxPosition());

    /*
     * if (Player.main.gameObject && LaserCutObject_SetCutFxPosition_Patch.IsCutByLocalPlayer(this))    <--- condition added
     * {
     *     Player.main.armsController.lookTargetTransform.position = this.matchingNodePos;
     * }
     */
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions).MatchEndForward([
                                                new CodeMatch(OpCodes.Ldsfld, Reflect.Field(() => Player.main)),
                                                new CodeMatch(OpCodes.Callvirt, Reflect.Property((Component t) => t.gameObject).GetMethod),
                                                new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Object), "op_Implicit"))
                                            ])
                                            .ThrowIfInvalid("Could not find the look target condition")
                                            .Advance(1)
                                            .InsertAndAdvance([
                                                new CodeInstruction(OpCodes.Ldarg_0),
                                                new CodeInstruction(OpCodes.Call, Reflect.Method(() => IsCutByLocalPlayer(default))),
                                                new CodeInstruction(OpCodes.And)
                                            ])
                                            .InstructionEnumeration();
    }

    public static bool IsCutByLocalPlayer(LaserCutObject laserCutObject)
    {
        return Inventory.main.GetHeldTool() is LaserCutter laserCutter && laserCutter.activeCuttingTarget == laserCutObject.sealedScript;
    }
}
