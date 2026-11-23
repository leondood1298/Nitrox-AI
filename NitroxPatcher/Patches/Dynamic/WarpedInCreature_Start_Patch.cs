using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A creature a warp ball brought in here is announced to the others (<see cref="SharedCreatureSpawns" />).
/// </summary>
public sealed partial class WarpedInCreature_Start_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((WarpedInCreature t) => t.Start());

    public static void Postfix(WarpedInCreature __instance) => Resolve<SharedCreatureSpawns>().Announce(__instance.gameObject);
}
