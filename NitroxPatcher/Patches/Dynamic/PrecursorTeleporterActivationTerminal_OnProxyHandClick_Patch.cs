using System.Reflection;
using NitroxClient.GameLogic.PlayerLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The terminal is only marked as unlocked when the inserting player's cinematic ends. Until then another player's
///     click would take that player's ion cube for nothing.
/// </summary>
public sealed partial class PrecursorTeleporterActivationTerminal_OnProxyHandClick_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PrecursorTeleporterActivationTerminal t) => t.OnProxyHandClick(default));

    public static bool Prefix(PrecursorTeleporterActivationTerminal __instance)
    {
        return !Resolve<PlayerCinematics>().IsUsedByOtherPlayer(__instance.cinematicController);
    }
}
