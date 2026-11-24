using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A void Ghost Leviathan the void made around this game's player is announced to the others (<see cref="SharedCreatureSpawns" />).
/// </summary>
public sealed partial class GhostLeviatanVoid_Start_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((GhostLeviatanVoid t) => t.Start());

    public static void Postfix(GhostLeviatanVoid __instance) => Resolve<SharedCreatureSpawns>().Announce(__instance.gameObject);
}
