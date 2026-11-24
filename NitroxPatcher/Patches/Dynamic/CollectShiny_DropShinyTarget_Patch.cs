using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Stalker that drops what it carries gives the item's lock back, see <see cref="StalkerShinyGrabs" />.
/// </summary>
public sealed partial class CollectShiny_DropShinyTarget_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((CollectShiny t) => t.DropShinyTarget(default(GameObject)));

    public static void Prefix(CollectShiny __instance, GameObject target) => Resolve<StalkerShinyGrabs>().Dropped(__instance, target);
}
