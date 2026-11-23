using System;
using System.Reflection;
using Nitrox.Model.Helper;
using NitroxClient.Extensions;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.PlayerLogic;
using NitroxClient.MonoBehaviours;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class PlayerCinematicController_OnPlayerCinematicModeEnd_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PlayerCinematicController t) => t.OnPlayerCinematicModeEnd());

    public static void Prefix(PlayerCinematicController __instance)
    {
        if (__instance.cinematicModeActive && __instance.TryGetComponentInParent<NitroxEntity>(out var component, includeInactive: true) && !component.gameObject.GetComponent<Bed>() && !__instance.GetComponentInParent<Bench>() && Resolve<LocalPlayer>().SessionId.HasValue)
        {
            int hashCode = __instance.gameObject.GetHierarchyPath(component.gameObject).GetHashCode();
            Resolve<PlayerCinematics>().EndCinematicMode(Resolve<LocalPlayer>().SessionId.Value, component.Id, hashCode, __instance.playerViewAnimationName);
            // The game calls EndCinematicMode after this; that call must not announce the same end again.
            PlayerCinematicController_EndCinematicMode_Patch.MarkEndAnnounced(__instance);
        }
    }
}
