using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A cure ball rides on the baby until the baby lets it go into the world. That is when the other players are given it.
/// </summary>
public sealed partial class DropEnzymes_ReleaseSpawnedCureBall_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((DropEnzymes t) => t.ReleaseSpawnedCureBall());

    public static void Prefix(DropEnzymes __instance, out GameObject __state)
    {
        // The method forgets the ball
        __state = __instance.spawnedCureBall;
    }

    public static void Postfix(DropEnzymes __instance, GameObject __state)
    {
        Resolve<EnzymeBalls>().Released(__instance, __state);
    }
}
