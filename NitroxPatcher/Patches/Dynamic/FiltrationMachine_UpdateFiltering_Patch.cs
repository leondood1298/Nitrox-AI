using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A water filtration machine's update runs on every client. Only the player simulating the machine draws base
///     power for it (see <see cref="BasePowerConsumptionForwarder.ConsumeEnergy" />). Its filtering progress is still
///     kept by each client on its own.
/// </summary>
public sealed partial class FiltrationMachine_UpdateFiltering_Patch : NitroxPatch, IDynamicPatch
{
    internal static readonly MethodInfo TARGET_METHOD = Reflect.Method((FiltrationMachine t) => t.UpdateFiltering());

    /*
     * PowerSystem.ConsumeEnergy(powerRelay, 0.85f * dayNightSpeed, out float _);
     * becomes
     * FiltrationMachine_UpdateFiltering_Patch.ConsumeEnergy(powerRelay, 0.85f * dayNightSpeed, out float _, this);
     */
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions).MatchStartForward(new CodeMatch(OpCodes.Call, Reflect.Method(() => PowerSystem.ConsumeEnergy(default, default, out Reflect.Ref<float>.Field))))
                                            .ThrowIfInvalid($"Could not find the power draw in {nameof(FiltrationMachine)}.{nameof(FiltrationMachine.UpdateFiltering)}")
                                            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                                            .SetOperandAndAdvance(Reflect.Method(() => ConsumeEnergy(default, default, out Reflect.Ref<float>.Field, default)))
                                            .InstructionEnumeration();
    }

    public static bool ConsumeEnergy(IPowerInterface powerInterface, float amount, out float amountConsumed, FiltrationMachine filtrationMachine)
    {
        return Resolve<BasePowerConsumptionForwarder>().ConsumeEnergy(filtrationMachine, powerInterface, amount, out amountConsumed);
    }
}
