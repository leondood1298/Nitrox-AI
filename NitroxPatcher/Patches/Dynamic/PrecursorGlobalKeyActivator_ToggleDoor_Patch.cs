using System.Reflection;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Precursor door that opens completes its goal on the goal manager, without StoryGoal.Execute. The other games open
///     the door when they get the goal (PrecursorGlobalKeyActivator.NotifyGoalComplete), and a joining one when it loads
///     the door (OnEnable).
/// </summary>
public sealed partial class PrecursorGlobalKeyActivator_ToggleDoor_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PrecursorGlobalKeyActivator t) => t.ToggleDoor(default));

    public static void Prefix(PrecursorGlobalKeyActivator __instance, out bool __state)
    {
        __state = StoryManager.IsGoalComplete(__instance.doorActivationKey);
    }

    public static void Postfix(PrecursorGlobalKeyActivator __instance, bool __state)
    {
        StoryManager.ReportGoalCompletedDirectly(Resolve<IPacketSender>(), __instance.doorActivationKey, __state);
    }
}
