using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Only the game of the Warper's simulating player brings creatures in where its ball bursts: the copies of the ball
///     elsewhere just vanish, and the creatures come as entities (<see cref="SharedCreatureSpawns" />).
/// </summary>
public sealed partial class WarpBall_Detonate_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((WarpBall t) => t.Detonate());

    public static void Prefix(WarpBall __instance)
    {
        // WarpBall.Detonate brings creatures in only once its detonation time has come
        if (!Resolve<SharedCreatureSpawns>().IsDecidedHere(__instance.warperGO))
        {
            __instance.detonationTime = float.MaxValue;
        }
    }
}
