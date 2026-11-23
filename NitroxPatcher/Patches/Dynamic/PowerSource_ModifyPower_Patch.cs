using System.Reflection;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Reports energy drawn from a base power source that another player simulates (see
///     <see cref="BasePowerConsumptionForwarder" />).
///     <para>
///     Every draw on a base ends here: PowerSystem.ConsumeEnergy calls the relay's ModifyPower with a negative amount,
///     and the relay spreads it over its sources. The out parameter cannot be used to measure the draw because the game
///     sets it to the requested amount, without touching the level, in modes that do not require power.
///     </para>
///     <para>
///     For the same reason a source simulated by a Creative player would never fill: the prefix applies what such a
///     source generates itself (see <see cref="BasePowerSources.MustGenerateWithoutPowerRequirement" />).
///     </para>
/// </summary>
public sealed partial class PowerSource_ModifyPower_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PowerSource t) => t.ModifyPower(default, out Reflect.Ref<float>.Field));

    public static bool Prefix(PowerSource __instance, float amount, ref float consumed, ref bool __result, out float __state)
    {
        __state = __instance.power;
        // Checked first: nearly every player's mode requires power, and then the game does all of this itself
        if (GameModeUtils.RequiresPower())
        {
            return true;
        }
        bool simulatedLocally = amount > 0f && __instance.TryGetNitroxId(out NitroxId sourceId) && Resolve<SimulationOwnership>().HasAnyLockType(sourceId);
        if (!BasePowerSources.MustGenerateWithoutPowerRequirement(false, simulatedLocally, amount))
        {
            return true;
        }

        // What the game returns in a mode without power requirements, so that its callers behave as they do there
        __result = amount <= __instance.maxPower - __instance.power;
        consumed = amount;
        __instance.power = BasePowerSources.LevelAfterGenerating(__instance.power, __instance.maxPower, amount);
        return false;
    }

    public static void Postfix(PowerSource __instance, float __state)
    {
        float consumed = __state - __instance.power;
        if (consumed <= 0f)
        {
            return;
        }
        BasePowerConsumptionForwarder forwarder = Resolve<BasePowerConsumptionForwarder>();
        if (forwarder.IsConsumerActive && __instance.TryGetNitroxId(out NitroxId sourceId))
        {
            forwarder.RecordConsumption(sourceId, consumed);
        }
    }
}
