using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Reports the oxygen left in a worn tank (see <see cref="OxygenTanks" />).
/// </summary>
public sealed partial class Oxygen_RemoveOxygen_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Oxygen t) => t.RemoveOxygen(default(float)));

    public static void Prefix(Oxygen __instance, out float __state)
    {
        __state = __instance.oxygenAvailable;
    }

    public static void Postfix(Oxygen __instance, float __state)
    {
        Resolve<OxygenTanks>().Changed(__instance, __state);
    }
}
