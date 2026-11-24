using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Seamoth's lights drain its cell in every game where it is loaded (ToggleLights.Update). Only the game simulating
///     the vehicle drains it and reports the charge; the lights going out on an empty cell reach the others with the
///     vehicle's metadata.
/// </summary>
public sealed partial class ToggleLights_UpdateLightEnergy_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((ToggleLights t) => t.UpdateLightEnergy());

    public static bool Prefix(ToggleLights __instance)
    {
        return !Resolve<Vehicles>().IsSimulatedElsewhere(__instance);
    }
}
