using System.Reflection;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Prevents non simulated Reaper Leviathan from grabbing seamoths.
/// </summary>
/// <remarks>
/// <see cref="ReaperLeviathan.GrabSeamoth(SeaMoth)"/> calls <see cref="ReaperLeviathan.GrabVehicle(Vehicle, ReaperLeviathan.VehicleType)"/>
/// so we only block the direct calls from <see cref="ReaperMeleeAttack"/>, but when necessary (network packet) we can directly use the "child" function.
/// </remarks>
public sealed partial class ReaperLeviathan_GrabSeamoth_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((ReaperLeviathan t) => t.GrabSeamoth(default));

    public static bool Prefix(ReaperLeviathan __instance)
    {
        return ReaperLeviathan_GrabVehicle_Patch.IsSimulatedLeviathan(__instance, out _);
    }
}
