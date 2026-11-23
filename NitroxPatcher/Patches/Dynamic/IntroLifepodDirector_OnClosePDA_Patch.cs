using System.Reflection;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Closing the PDA the intro opened completes this goal on the goal manager, without StoryGoal.Execute.
/// </summary>
public sealed partial class IntroLifepodDirector_OnClosePDA_Patch : NitroxPatch, IDynamicPatch
{
    private const string GOAL_KEY = "Trigger_PDAIntroEnd";

    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((IntroLifepodDirector t) => t.OnClosePDA(default));

    public static void Prefix(out bool __state)
    {
        __state = StoryManager.IsGoalComplete(GOAL_KEY);
    }

    public static void Postfix(bool __state)
    {
        StoryManager.ReportGoalCompletedDirectly(Resolve<IPacketSender>(), GOAL_KEY, __state);
    }
}
