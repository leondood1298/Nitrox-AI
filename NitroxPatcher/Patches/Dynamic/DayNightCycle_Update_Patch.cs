using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Replace the local time calculations by the real server time, except while this game plays the rocket's ending.
/// </summary>
public sealed partial class DayNightCycle_Update_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((DayNightCycle t) => t.Update());

    public static bool Prefix(DayNightCycle __instance)
    {
        // The ending has set and paused the time of day for its sky; the game's own update keeps it there
        if (Resolve<TimeManager>().IsTimeDecidedByEnding)
        {
            return true;
        }

        __instance.timePassedAsDouble = Resolve<TimeManager>().CalculateCurrentTime();

        if (__instance.IsInSkipTimeMode() && __instance.timePassed >= __instance.skipModeEndTime)
        {
            __instance.StopSkipTimeMode();
        }

        __instance.UpdateAtmosphere();
        __instance.UpdateDayNightMessage();
        return false;
    }
}
