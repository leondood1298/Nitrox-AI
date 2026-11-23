using System;
using System.Reflection;
using Nitrox.Model.Helper;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class BaseNuclearReactor_Update_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((BaseNuclearReactor t) => t.Update());

    public static void Postfix(BaseNuclearReactor __instance)
    {
        BasePowerBroadcaster.BroadcastIfOwner(__instance, __instance._powerSource, Resolve<SimulationOwnership>(), Resolve<BasePowerState>(), Resolve<IPacketSender>());
    }
}
