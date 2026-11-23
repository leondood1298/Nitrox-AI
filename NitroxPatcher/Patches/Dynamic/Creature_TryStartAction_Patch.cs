using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The game starts an action out of turn when a creature is scared (Scareable, FlyAwayWhenScared), hit (FleeOnDamage,
///     WarpOut) or lands (MoveOnGround, DescendToGround). On a copy of another player's creature that stopped, started and
///     performed the action on its own, past the gate of Creature.UpdateBehaviour (see <see cref="AI.MayChooseAction" />):
///     a copy fled or warped out where its simulating player's creature did not.
/// </summary>
public sealed partial class Creature_TryStartAction_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Creature t) => t.TryStartAction(default));

    public static bool Prefix(Creature __instance, ref bool __result)
    {
        if (Resolve<AI>().MayChooseAction(__instance))
        {
            return true;
        }
        __result = false;
        return false;
    }
}
