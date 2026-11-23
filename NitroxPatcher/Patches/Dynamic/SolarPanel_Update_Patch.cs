using System.Reflection;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class SolarPanel_Update_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((SolarPanel t) => t.Update());

    public static bool Prefix(SolarPanel __instance)
    {
        if (__instance.TryGetNitroxId(out NitroxId nitroxId))
        {
            return Resolve<SimulationOwnership>().HasAnyLockType(nitroxId);
        }
        return true;
    }

    public static void Postfix(SolarPanel __instance)
    {
        Resolve<BasePowerSources>().BroadcastIfSimulated(__instance, __instance.powerSource);
    }
}
