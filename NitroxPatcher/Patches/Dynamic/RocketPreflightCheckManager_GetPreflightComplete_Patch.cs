using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The game answers a rocket's time capsule preflight check from its single time capsule. With several rockets, a
///     rocket whose capsule that one does not show answers from its own.
/// </summary>
public sealed partial class RocketPreflightCheckManager_GetPreflightComplete_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((RocketPreflightCheckManager t) => t.GetPreflightComplete(default));

    public static bool Prefix(RocketPreflightCheckManager __instance, PreflightCheck preflightCheck, ref bool __result)
    {
        return preflightCheck != PreflightCheck.TimeCapsule || !Resolve<TimeCapsules>().TryGetPutAwayPreflight(__instance, out __result);
    }
}
