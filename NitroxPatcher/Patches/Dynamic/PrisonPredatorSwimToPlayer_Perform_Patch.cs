using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     An aquarium predator swims at the player its Evaluate chose, which need not be this game's (<see cref="PrisonPredators" />).
/// </summary>
public sealed partial class PrisonPredatorSwimToPlayer_Perform_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PrisonPredatorSwimToPlayer t) => t.Perform(default(Creature), default(float), default(float)));

    public static bool Prefix(PrisonPredatorSwimToPlayer __instance, Creature creature, float time)
    {
        Resolve<PrisonPredators>().Perform(__instance, creature, time);
        return false;
    }
}
