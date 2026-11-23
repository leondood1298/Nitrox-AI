using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Stalker ate a fish, perhaps from the local player's hand, see <see cref="StalkerFriendships" />.
/// </summary>
public sealed partial class Stalker_OnFishEat_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Stalker t) => t.OnFishEat(default(GameObject)));

    public static void Prefix(Stalker __instance, GameObject fish) => Resolve<StalkerFriendships>().FishEaten(__instance, fish);
}
