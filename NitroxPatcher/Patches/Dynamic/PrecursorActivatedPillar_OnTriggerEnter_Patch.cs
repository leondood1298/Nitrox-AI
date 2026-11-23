using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Lets the trigger react to other players too, and keeps it open while anybody is still inside.
/// </summary>
public sealed partial class PrecursorActivatedPillar_OnTriggerEnter_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PrecursorActivatedPillar t) => t.OnTriggerEnter(default));

    public static bool Prefix(PrecursorActivatedPillar __instance, Collider col)
    {
        return Resolve<PrecursorProximityTriggers>().PillarEntered(__instance, col);
    }
}