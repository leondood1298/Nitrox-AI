using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A cure ball that touches an infected creature cures it and is used up. Only the creature's simulating player may cure
///     it, so the touch counts in that player's game, and the ball is then removed for everybody.
/// </summary>
public sealed partial class EnzymeBall_OnTriggerEnter_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((EnzymeBall t) => t.OnTriggerEnter(default));

    public static bool Prefix(Collider col, out bool __state)
    {
        return Resolve<EnzymeBalls>().MayCure(col, out __state);
    }

    public static void Postfix(EnzymeBall __instance, bool __state)
    {
        Resolve<EnzymeBalls>().OnTouched(__instance, __state);
    }
}
