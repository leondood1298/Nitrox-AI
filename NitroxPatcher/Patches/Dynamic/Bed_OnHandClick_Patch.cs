using System;
using System.Reflection;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Helper;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.Extensions;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.Simulation;
using NitroxClient.MonoBehaviours.Gui.HUD;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class Bed_OnHandClick_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((Bed t) => t.OnHandClick(null));

    private static bool skipPrefix;

    public static bool Prefix(Bed __instance, GUIHand hand)
    {
        if (skipPrefix)
        {
            return true;
        }
        if (!__instance.TryGetIdOrWarn(out NitroxId nitroxId))
        {
            return true;
        }
        if (Resolve<SimulationOwnership>().HasExclusiveLock(nitroxId))
        {
            return true;
        }
        HandInteraction<Bed> context = new HandInteraction<Bed>(__instance, hand);
        LockRequest<HandInteraction<Bed>> lockRequest = new LockRequest<HandInteraction<Bed>>(nitroxId, SimulationLockType.EXCLUSIVE, ReceivedSimulationLockResponse, context);
        Resolve<SimulationOwnership>().RequestSimulationLock(lockRequest);
        return false;
    }

    private static void ReceivedSimulationLockResponse(NitroxId bedId, bool lockAcquired, HandInteraction<Bed> context)
    {
        Bed target = context.Target;
        if (lockAcquired)
        {
            skipPrefix = true;
            try
            {
                target.OnHandClick(context.GuiHand);
            }
            finally
            {
                skipPrefix = false;
            }
            // The game ignores the click when the player cannot sleep yet or stands on neither side of the bed.
            // Announcing the lie-down or keeping the exclusive lock then would show the player in bed to others
            // and block the bed until this player disconnects, because the lock is only released on waking up.
            if (!target.cinematicController || !target.cinematicController.cinematicModeActive)
            {
                Resolve<SimulationOwnership>().RequestSimulationLock(bedId, SimulationLockType.TRANSIENT);
                return;
            }
            string animationKey = ((target.cinematicController == target.leftLieDownCinematicController) ? "bed_down_left" : "bed_down_right");
            Resolve<IPacketSender>().Send(new BedEnterAnimation(Resolve<LocalPlayer>().SessionId.Value, bedId, animationKey));
        }
        else
        {
            target.gameObject.AddComponent<DenyOwnershipHand>();
            ErrorMessage.AddMessage("Another player is using this bed");
        }
    }
}
