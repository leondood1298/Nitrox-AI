using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Warper its spawner made here is announced to the others (<see cref="SharedCreatureSpawns" />).
/// </summary>
public sealed partial class Warper_WarpIn_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Warper t) => t.WarpIn(default));

    public static void Postfix(Warper __instance) => Resolve<SharedCreatureSpawns>().Announce(__instance.gameObject);
}
