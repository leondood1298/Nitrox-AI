using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Mesmer starting to hypnotize another player tells that player's game (<see cref="CreatureEffects" />).
/// </summary>
public sealed partial class PullCreatures_StartPerform_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PullCreatures t) => t.StartPerform(default, default));

    public static void Postfix(PullCreatures __instance) => Resolve<CreatureEffects>().HypnosisChanged(__instance, true);
}
