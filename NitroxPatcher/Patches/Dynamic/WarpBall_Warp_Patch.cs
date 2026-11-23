using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The ball of another player's Warper warps only this game's player (<see cref="SharedCreatureSpawns.MayWarp" />).
/// </summary>
public sealed partial class WarpBall_Warp_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((WarpBall t) => t.Warp(default(GameObject), default(Vector3)));

    public static bool Prefix(WarpBall __instance, GameObject target) => Resolve<SharedCreatureSpawns>().MayWarp(__instance, target);
}
