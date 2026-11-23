using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Mesmer may hypnotize another player, not only its simulating player (<see cref="CreatureEffects" />).
/// </summary>
public sealed partial class PullCreatures_IsValidTarget_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PullCreatures t) => t.IsValidTarget(default(GameObject)));

    public static void Postfix(PullCreatures __instance, GameObject target, ref bool __result)
    {
        __result = Resolve<CreatureEffects>().IsValidHypnosisTarget(__instance, target, __result);
    }
}
