using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Disables the Sunbeam gun in every game where its goal completes, not only in the game of the player at the terminal.
/// </summary>
public sealed partial class StoryGoalCustomEventHandler_NotifyGoalComplete_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((StoryGoalCustomEventHandler t) => t.NotifyGoalComplete(default));

    public static void Postfix(StoryGoalCustomEventHandler __instance, string key) => StoryManager.OnCustomEventGoalCompleted(__instance, key);
}
