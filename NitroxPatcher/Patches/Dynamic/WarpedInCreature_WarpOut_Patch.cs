using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A warped-in creature whose time ran out is gone for everybody (<see cref="SharedCreatureSpawns" />).
/// </summary>
public sealed partial class WarpedInCreature_WarpOut_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((WarpedInCreature t) => t.WarpOut());

    public static void Prefix(WarpedInCreature __instance) => Resolve<SharedCreatureSpawns>().Ending(__instance.gameObject);
}
