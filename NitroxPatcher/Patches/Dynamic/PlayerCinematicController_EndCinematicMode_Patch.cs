using System.Collections.Generic;
using System.Reflection;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.PlayerLogic;
using NitroxClient.MonoBehaviours;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Tells other players that the local player's cinematic ended when the game cuts it short.
///     <p />
///     A cinematic normally finishes through OnPlayerCinematicModeEnd (an animation event), which
///     <see cref="PlayerCinematicController_OnPlayerCinematicModeEnd_Patch" /> announces. OnDisable and OnPlayerDeath
///     call EndCinematicMode directly instead. Without an announcement the other players keep this player in the
///     cinematic pose, because a remote player in a cinematic ignores movement until told it is over.
/// </summary>
public sealed partial class PlayerCinematicController_EndCinematicMode_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PlayerCinematicController t) => t.EndCinematicMode());

    /// <summary>
    ///     Controllers whose current cinematic has already been announced as ended, by Unity instance id.
    /// </summary>
    private static readonly HashSet<int> announcedEnds = [];

    /// <summary>
    ///     Records that the end of <paramref name="controller" />'s current cinematic has been sent, so the
    ///     EndCinematicMode call that follows the normal end does not send it again.
    /// </summary>
    public static void MarkEndAnnounced(PlayerCinematicController controller) => announcedEnds.Add(controller.GetInstanceID());

    public static void Prefix(PlayerCinematicController __instance)
    {
        bool alreadyAnnounced = announcedEnds.Remove(__instance.GetInstanceID());

        // The game ignores this call when no cinematic is active. Also skip while leaving the session: this runs
        // from OnDisable when objects are torn down.
        if (alreadyAnnounced || !__instance.cinematicModeActive || !Multiplayer.Joined)
        {
            return;
        }
        if (!__instance.TryGetComponentInParent(out NitroxEntity entity, true) || entity.gameObject.GetComponent<Bed>())
        {
            return;
        }
        LocalPlayer localPlayer = Resolve<LocalPlayer>();
        if (!localPlayer.SessionId.HasValue)
        {
            return;
        }

        int controllerNameHash = __instance.gameObject.GetHierarchyPath(entity.gameObject).GetHashCode();
        Resolve<PlayerCinematics>().EndCinematicMode(localPlayer.SessionId.Value, entity.Id, controllerNameHash, __instance.playerViewAnimationName);
    }
}
