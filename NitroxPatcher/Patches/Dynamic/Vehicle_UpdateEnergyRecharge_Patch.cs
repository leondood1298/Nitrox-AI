using System.Reflection;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Ensures vehicle charging from moonpool only occurs on the simulating player, and reports the base power it draws to
/// whoever simulates the power sources (see <see cref="BasePowerConsumptionForwarder" />).
/// </summary>
public sealed partial class Vehicle_UpdateEnergyRecharge_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Vehicle t) => t.UpdateEnergyRecharge());

    public static bool Prefix(Vehicle __instance, out bool __state)
    {
        __state = __instance.TryGetNitroxId(out NitroxId vehicleId) && Resolve<SimulationOwnership>().HasAnyLockType(vehicleId);
        if (__state)
        {
            Resolve<BasePowerConsumptionForwarder>().EnterConsumer();
        }
        return __state;
    }

    public static void Finalizer(bool __state)
    {
        if (__state)
        {
            Resolve<BasePowerConsumptionForwarder>().ExitConsumer();
        }
    }
}
