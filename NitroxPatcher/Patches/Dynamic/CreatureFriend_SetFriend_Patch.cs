using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Stalker's friend changed, see <see cref="StalkerFriendships" />.
/// </summary>
public sealed partial class CreatureFriend_SetFriend_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((CreatureFriend t) => t.SetFriend(default(GameObject), default(float)));

    public static void Postfix(CreatureFriend __instance) => Resolve<StalkerFriendships>().FriendChanged(__instance);
}
