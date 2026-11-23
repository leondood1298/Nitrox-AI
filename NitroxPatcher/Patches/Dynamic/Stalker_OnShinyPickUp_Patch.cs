using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Stalker smells what it picked up, which may have been dropped by another player, see <see cref="StalkerFriendships" />.
/// </summary>
public sealed partial class Stalker_OnShinyPickUp_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Stalker t) => t.OnShinyPickUp(default(object)));

    public static void Prefix(object target) => Resolve<StalkerFriendships>().ShinyPickingUp(target);
}
