using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     An aquarium predator goes for any player at the glass, not only the one simulating it (<see cref="PrisonPredators" />).
/// </summary>
public sealed partial class PrisonPredatorSwimToPlayer_Evaluate_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PrisonPredatorSwimToPlayer t) => t.Evaluate(default(Creature), default(float)));

    public static bool Prefix(PrisonPredatorSwimToPlayer __instance, Creature creature, float time, ref float __result)
    {
        __result = Resolve<PrisonPredators>().Evaluate(__instance, creature, time);
        return false;
    }
}
