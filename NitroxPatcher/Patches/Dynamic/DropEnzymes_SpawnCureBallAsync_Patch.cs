using System.Reflection;
using HarmonyLib;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A cure ball rides on the baby until the baby lets it go, and a player may click it (the cure) all that time. It is
///     given to the other players as soon as it is made, so that its cure cinematic is played under its own id.
/// </summary>
public sealed partial class DropEnzymes_SpawnCureBallAsync_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = AccessTools.EnumeratorMoveNext(Reflect.Method((DropEnzymes t) => t.SpawnCureBallAsync()));

    public static void Postfix(object __instance, bool __result)
    {
        // The coroutine ends once the ball sits on the baby
        if (!__result)
        {
            Resolve<EnzymeBalls>().Spawned(Traverse.Create(__instance).Field("<>4__this").GetValue<DropEnzymes>());
        }
    }
}
