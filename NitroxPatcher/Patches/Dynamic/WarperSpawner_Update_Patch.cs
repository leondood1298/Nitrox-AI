using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Only the game of the player nearest a Warper spawner makes its Warpers (<see cref="SharedCreatureSpawns" />).
/// </summary>
public sealed partial class WarperSpawner_Update_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((WarperSpawner t) => t.Update());

    public static bool Prefix(WarperSpawner __instance) => Resolve<SharedCreatureSpawns>().MaySpawnAt(__instance.transform.position);
}
