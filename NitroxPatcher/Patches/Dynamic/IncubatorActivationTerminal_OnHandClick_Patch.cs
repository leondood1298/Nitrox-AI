using System.Reflection;
using NitroxClient.GameLogic.PlayerLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The incubator is only powered when the inserting player's cinematic ends. Until then another player's click would
///     take that player's ion cube for nothing.
/// </summary>
public sealed partial class IncubatorActivationTerminal_OnHandClick_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((IncubatorActivationTerminal t) => t.OnHandClick(default));

    public static bool Prefix(IncubatorActivationTerminal __instance)
    {
        return !Resolve<PlayerCinematics>().IsUsedByOtherPlayer(__instance.cinematicController);
    }
}
