using System.Reflection;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.Extensions;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class Bed_ExitInUseMode_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((Bed t) => t.ExitInUseMode(default(Player), default(bool)));

    public static void Prefix(Bed __instance)
    {
        if (__instance.inUseMode == Bed.InUseMode.Sleeping)
        {
            string animationKey = ((__instance.currentStandUpCinematicController == __instance.leftStandUpCinematicController) ? "bed_up_left" : "bed_up_right");
            if (__instance.TryGetNitroxId(out NitroxId nitroxId))
            {
                Resolve<IPacketSender>().Send(new BedExitAnimation(Resolve<LocalPlayer>().SessionId.Value, nitroxId, animationKey));
                Resolve<SimulationOwnership>().RequestSimulationLock(nitroxId, SimulationLockType.TRANSIENT);
            }
        }
    }
}
