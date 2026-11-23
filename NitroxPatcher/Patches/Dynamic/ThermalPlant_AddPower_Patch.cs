using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class ThermalPlant_AddPower_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((ThermalPlant t) => t.AddPower());

    public static bool Prefix(ThermalPlant __instance) => Resolve<BasePowerSources>().GeneratesHere(__instance);

    public static void Postfix(ThermalPlant __instance) => Resolve<BasePowerSources>().BroadcastIfSimulated(__instance, __instance.powerSource);
}
