using System.Reflection;
using Nitrox.Model.DataStructures;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class ThermalPlant_AddPower_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((ThermalPlant t) => t.AddPower());

    public static bool Prefix(ThermalPlant __instance)
    {
        return !__instance.TryGetNitroxId(out NitroxId id) || Resolve<SimulationOwnership>().HasAnyLockType(id);
    }

    public static void Postfix(ThermalPlant __instance)
    {
        BasePowerBroadcaster.BroadcastIfOwner(__instance, __instance.powerSource, Resolve<SimulationOwnership>(), Resolve<BasePowerState>(), Resolve<IPacketSender>());
    }
}
