using System.Reflection;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The end of the intro opens the PDA and completes this goal on the goal manager, without StoryGoal.Execute.
/// </summary>
public sealed partial class IntroLifepodDirector_OpenPDA_Patch : NitroxPatch, IDynamicPatch
{
    private const string GOAL_KEY = "Trigger_PDAIntroBegin";

    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((IntroLifepodDirector t) => t.OpenPDA());

    public static void Prefix(out bool __state)
    {
        __state = StoryManager.IsGoalComplete(GOAL_KEY);
    }

    public static void Postfix(bool __state)
    {
        StoryManager.ReportGoalCompletedDirectly(Resolve<IPacketSender>(), GOAL_KEY, __state);
    }
}
