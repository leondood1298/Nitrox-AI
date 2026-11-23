using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Gives the other players the encyclopedia entry of a found time capsule this player collects.
/// </summary>
public sealed partial class TimeCapsule_Collect_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((TimeCapsule t) => t.Collect());

    public static void Prefix(TimeCapsule __instance)
    {
        Resolve<TimeCapsules>().BroadcastCollected(__instance);
    }
}
