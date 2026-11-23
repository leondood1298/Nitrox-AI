using System.Reflection;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class BaseBioReactor_Update_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((BaseBioReactor t) => t.Update());

    public static void Postfix(BaseBioReactor __instance)
    {
        BasePowerBroadcaster.BroadcastIfOwner(__instance, __instance._powerSource, Resolve<SimulationOwnership>(), Resolve<BasePowerState>(), Resolve<IPacketSender>());
    }
}
