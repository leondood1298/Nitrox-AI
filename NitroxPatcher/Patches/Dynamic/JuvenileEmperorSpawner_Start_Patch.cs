using System.Collections;
using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The Sea Emperor Juvenile is made once in the world, by the nearest player, and every game sees it
///     (<see cref="SharedCreatureSpawns.SpawnJuvenile" />). Each game made its own, and again on every load.
/// </summary>
public sealed partial class JuvenileEmperorSpawner_Start_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((JuvenileEmperorSpawner t) => t.Start());

    public static bool Prefix(JuvenileEmperorSpawner __instance, ref IEnumerator __result)
    {
        __result = Resolve<SharedCreatureSpawns>().SpawnJuvenile(__instance);
        return false;
    }
}
