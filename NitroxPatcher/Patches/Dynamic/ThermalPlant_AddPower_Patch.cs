using System;
using System.CodeDom.Compiler;
using System.Reflection;
using HarmonyLib;
using Nitrox.Model.Helper;
using NitroxClient.Communication.Abstract;
using NitroxClient.Extensions;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed class ThermalPlant_AddPower_Patch : NitroxPatch, IDynamicPatch, INitroxPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((ThermalPlant t) => t.AddPower());

    public static bool Prefix(ThermalPlant __instance)
    {
        return !__instance.TryGetNitroxId(out var id) || NitroxPatch.Resolve<SimulationOwnership>().HasAnyLockType(id);
    }

    public static void Postfix(ThermalPlant __instance)
    {
        BasePowerBroadcaster.BroadcastIfOwner(__instance, __instance.powerSource, NitroxPatch.Resolve<SimulationOwnership>(), NitroxPatch.Resolve<BasePowerState>(), NitroxPatch.Resolve<IPacketSender>());
    }

    [GeneratedCode("Nitrox.Analyzers", "1.0.13.0")]
    public override void Patch(Harmony harmony)
    {
        PatchMultiple(harmony, TARGET_METHOD, new Func<ThermalPlant, bool>(Prefix).Method, new Action<ThermalPlant>(Postfix).Method);
    }
}
