using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The cutting effect follows the cut and points the local player's arms at it. When another player cuts, the effect is
///     shown here as well: on the side of the object where that player cuts, and the local player's arms must stay where
///     they are.
/// </summary>
public sealed partial class LaserCutObject_SetCutFxPosition_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((LaserCutObject t) => t.SetCutFxPosition());

    /*
     * bool flag = Utils.CheckObjectInFront(this.transform, LaserCutObject_SetCutFxPosition_Patch.GetCutterTransform(this), 90f);    <--- was Player.main.transform
     * ...
     * if (Player.main.gameObject && LaserCutObject_SetCutFxPosition_Patch.IsCutByLocalPlayer(this))    <--- condition added
     * {
     *     Player.main.armsController.lookTargetTransform.position = this.matchingNodePos;
     * }
     */
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions).MatchStartForward([
                                                new CodeMatch(OpCodes.Ldsfld, Reflect.Field(() => Player.main)),
                                                new CodeMatch(OpCodes.Callvirt, Reflect.Property((Component t) => t.transform).GetMethod),
                                                new CodeMatch(OpCodes.Ldc_R4, 90f),
                                                new CodeMatch(OpCodes.Call, Reflect.Method(() => Utils.CheckObjectInFront(default, default, default)))
                                            ])
                                            .ThrowIfInvalid("Could not find the side check")
                                            .SetAndAdvance(OpCodes.Ldarg_0, null)
                                            .SetAndAdvance(OpCodes.Call, Reflect.Method(() => GetCutterTransform(default)))
                                            .MatchEndForward([
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

    /// <summary>
    ///     The game puts the effect on the side of the object that faces the local player. Another player's cut is
    ///     shown on the side of the nearest player who holds a laser cutter.
    /// </summary>
    public static Transform GetCutterTransform(LaserCutObject laserCutObject)
    {
        if (IsCutByLocalPlayer(laserCutObject))
        {
            return Player.main.transform;
        }
        Transform? cutterHand = Resolve<PlayerManager>().FindNearestHandHolding<LaserCutter>(laserCutObject.transform.position);
        return cutterHand ? cutterHand : Player.main.transform;
    }
}
