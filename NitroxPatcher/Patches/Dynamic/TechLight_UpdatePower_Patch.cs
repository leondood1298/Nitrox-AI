using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A floodlight's power update runs on every client. Only the player simulating the floodlight draws base power for
///     it (see <see cref="BasePowerConsumptionForwarder.ConsumeEnergy" />); everyone still switches the light with the
///     base's power state.
/// </summary>
public sealed partial class TechLight_UpdatePower_Patch : NitroxPatch, IDynamicPatch
{
    internal static readonly MethodInfo TARGET_METHOD = Reflect.Method((TechLight t) => t.UpdatePower());

    /*
     * PowerSystem.ConsumeEnergy(this.powerRelay, TechLight.powerPerSecond * TechLight.updateInterval, out float _);
     * becomes
     * TechLight_UpdatePower_Patch.ConsumeEnergy(this.powerRelay, TechLight.powerPerSecond * TechLight.updateInterval, out float _, this);
     */
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions).MatchStartForward(new CodeMatch(OpCodes.Call, Reflect.Method(() => PowerSystem.ConsumeEnergy(default, default, out Reflect.Ref<float>.Field))))
                                            .ThrowIfInvalid($"Could not find the power draw in {nameof(TechLight)}.{nameof(TechLight.UpdatePower)}")
                                            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                                            .SetOperandAndAdvance(Reflect.Method(() => ConsumeEnergy(default, default, out Reflect.Ref<float>.Field, default)))
                                            .InstructionEnumeration();
    }

    public static bool ConsumeEnergy(IPowerInterface powerInterface, float amount, out float amountConsumed, TechLight techLight)
    {
        // The floodlight's Nitrox id is on its Constructable, which the game has already checked at this point
        return Resolve<BasePowerConsumptionForwarder>().ConsumeEnergy(techLight.constructable, powerInterface, amount, out amountConsumed);
    }
}
