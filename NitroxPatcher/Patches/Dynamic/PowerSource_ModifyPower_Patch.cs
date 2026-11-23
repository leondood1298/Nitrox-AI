using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Reports energy drawn from a base power source that another player simulates (see
///     <see cref="BasePowerConsumptionForwarder.SourceLevelChanged" />).
///     <para>
///     The game leaves a source's level alone in modes that do not require power, so a source simulated by a Creative
///     player would never fill: the prefix applies what such a source generates itself (see
///     <see cref="BasePowerSources.TryGenerateWithoutPowerRequirement" />).
///     </para>
/// </summary>
public sealed partial class PowerSource_ModifyPower_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PowerSource t) => t.ModifyPower(default, out Reflect.Ref<float>.Field));

    public static bool Prefix(PowerSource __instance, float amount, ref float consumed, ref bool __result, out float __state)
    {
        __state = __instance.power;
        return !Resolve<BasePowerSources>().TryGenerateWithoutPowerRequirement(__instance, amount, ref consumed, ref __result);
    }

    public static void Postfix(PowerSource __instance, float __state) => Resolve<BasePowerConsumptionForwarder>().SourceLevelChanged(__instance, __state);
}
