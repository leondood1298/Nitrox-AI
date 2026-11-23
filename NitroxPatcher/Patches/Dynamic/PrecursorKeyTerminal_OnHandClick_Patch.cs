using System.Reflection;
using NitroxClient.GameLogic.PlayerLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The terminal is only marked as used when the inserting player's cinematic ends. Until then another player's click
///     would take that player's tablet for nothing.
/// </summary>
public sealed partial class PrecursorKeyTerminal_OnHandClick_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PrecursorKeyTerminal t) => t.OnHandClick(default));

    public static bool Prefix(PrecursorKeyTerminal __instance)
    {
        return !Resolve<PlayerCinematics>().IsUsedByOtherPlayer(__instance.cinematicController);
    }
}
