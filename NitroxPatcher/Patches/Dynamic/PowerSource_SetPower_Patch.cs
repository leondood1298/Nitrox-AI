using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     RegeneratePowerSource refills a Lifepod cell through PowerSource.SetPower. Only the player who simulates the cell
///     refills it and tells the others its level (see <see cref="BasePowerSources.SetsPowerHere(PowerSource)" />).
/// </summary>
public sealed partial class PowerSource_SetPower_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PowerSource t) => t.SetPower(default));

    public static bool Prefix(PowerSource __instance) => Resolve<BasePowerSources>().SetsPowerHere(__instance);

    public static void Postfix(PowerSource __instance, bool __runOriginal)
    {
        if (__runOriginal)
        {
            Resolve<BasePowerSources>().BroadcastIfSimulatedCell(__instance);
        }
    }
}
