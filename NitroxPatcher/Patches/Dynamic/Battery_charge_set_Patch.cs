using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Reports a battery's new charge (see <see cref="BatteryCharges" />). It is read after it was set: read before, the
///     server would always keep the charge from one step earlier, and a full battery would be stored short of full.
/// </summary>
public sealed partial class Battery_charge_set_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Property((Battery t) => t.charge).SetMethod;

    public static void Prefix(Battery __instance, out float __state)
    {
        __state = __instance.charge;
    }

    public static void Postfix(Battery __instance, float __state)
    {
        // The setter runs every frame for every battery in use; most changes are not reported, and that is the cheap test
        if (!BatteryCharges.ShouldReport(__state, __instance.charge, __instance.capacity))
        {
            return;
        }
        // A vehicle's cells are drained and charged in the game simulating the vehicle, which reports them
        if (Resolve<Vehicles>().IsSimulatedElsewhere(__instance))
        {
            return;
        }
        Resolve<BatteryCharges>().Changed(__instance, __state);
    }
}
