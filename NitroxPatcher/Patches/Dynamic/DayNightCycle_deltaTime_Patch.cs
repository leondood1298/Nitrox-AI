using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Replace the deltaTime calculations by one that is not capped by <see cref="Time.maximumDeltaTime"/>, except while this
/// game plays the rocket's ending.
/// </summary>
public sealed partial class DayNightCycle_deltaTime_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Property((DayNightCycle t) => t.deltaTime).GetGetMethod();

    public static bool Prefix(DayNightCycle __instance, out float __result)
    {
        TimeManager timeManager = Resolve<TimeManager>();
        __result = timeManager.DeltaTime;
        // The game's own value is 0 while the ending has paused the time of day
        return timeManager.IsTimeDecidedByEnding;
    }
}
