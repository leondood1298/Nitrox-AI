using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Warper that warps out is gone for everybody (<see cref="SharedCreatureSpawns" />).
/// </summary>
public sealed partial class Warper_EndWarpOut_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Warper t) => t.EndWarpOut());

    public static void Prefix(Warper __instance) => Resolve<SharedCreatureSpawns>().Ending(__instance.gameObject);
}
