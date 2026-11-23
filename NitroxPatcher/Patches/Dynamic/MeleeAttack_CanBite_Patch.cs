using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A copy of another player's Stalker or Sand Shark shows the bites that player's game sends instead of biting on its
///     own, see <see cref="CreatureBites" />.
/// </summary>
public sealed partial class MeleeAttack_CanBite_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MeleeAttack t) => t.CanBite(default(GameObject)));

    public static bool Prefix(MeleeAttack __instance, ref bool __result)
    {
        if (Resolve<CreatureBites>().MayBite(__instance))
        {
            return true;
        }
        __result = false;
        return false;
    }
}
