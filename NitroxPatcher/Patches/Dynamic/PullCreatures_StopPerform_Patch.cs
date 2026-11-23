using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Mesmer letting another player go tells that player's game, before StopPerform forgets whom it held (<see cref="CreatureEffects" />).
/// </summary>
public sealed partial class PullCreatures_StopPerform_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PullCreatures t) => t.StopPerform(default, default));

    public static void Prefix(PullCreatures __instance) => Resolve<CreatureEffects>().HypnosisChanged(__instance, false);
}
