using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A spotlight's power update runs on every client. Only the player simulating the spotlight draws base power for
///     it (see <see cref="BasePowerConsumptionForwarder.ConsumeEnergy" />); everyone still switches the light with the
///     base's power state.
/// </summary>
public sealed partial class BaseSpotLight_UpdatePower_Patch : NitroxPatch, IDynamicPatch
{
    internal static readonly MethodInfo TARGET_METHOD = Reflect.Method((BaseSpotLight t) => t.UpdatePower());

    /*
     * PowerSystem.ConsumeEnergy(this.powerRelay, BaseSpotLight.powerPerSecond * BaseSpotLight.updateInterval, out float _);
     * becomes
     * BaseSpotLight_UpdatePower_Patch.ConsumeEnergy(this.powerRelay, BaseSpotLight.powerPerSecond * BaseSpotLight.updateInterval, out float _, this);
     */
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions).MatchStartForward(new CodeMatch(OpCodes.Call, Reflect.Method(() => PowerSystem.ConsumeEnergy(default, default, out Reflect.Ref<float>.Field))))
                                            .ThrowIfInvalid($"Could not find the power draw in {nameof(BaseSpotLight)}.{nameof(BaseSpotLight.UpdatePower)}")
                                            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                                            .SetOperandAndAdvance(Reflect.Method(() => ConsumeEnergy(default, default, out Reflect.Ref<float>.Field, default)))
                                            .InstructionEnumeration();
    }

    public static bool ConsumeEnergy(IPowerInterface powerInterface, float amount, out float amountConsumed, BaseSpotLight spotLight)
    {
        return Resolve<BasePowerConsumptionForwarder>().ConsumeEnergy(spotLight, powerInterface, amount, out amountConsumed);
    }
}
