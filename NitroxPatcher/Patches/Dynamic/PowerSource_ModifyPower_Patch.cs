using System.Reflection;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Reports energy drawn from a base power source that another player simulates (see
///     <see cref="BasePowerConsumptionForwarder" />).
///     <p />
///     Every draw on a base ends here: PowerSystem.ConsumeEnergy calls the relay's ModifyPower with a negative amount,
///     and the relay spreads it over its sources. The out parameter cannot be used to measure the draw because the game
///     sets it to the requested amount, without touching the level, in modes that do not require power.
/// </summary>
public sealed partial class PowerSource_ModifyPower_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PowerSource t) => t.ModifyPower(default, out Reflect.Ref<float>.Field));

    public static void Prefix(PowerSource __instance, out float __state)
    {
        __state = __instance.power;
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
