using System.Reflection;
using Nitrox.Model.Helper;
using Nitrox.Model.Logger;
using NitroxClient.Extensions;
using NitroxClient.MonoBehaviours;
using NitroxClient.MonoBehaviours.CinematicController;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class PlayerCinematicController_Start_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PlayerCinematicController t) => t.Start());

    public static void Postfix(PlayerCinematicController __instance)
    {
        if (!__instance.TryGetComponentInParent<NitroxEntity>(out var component, includeInactive: true))
        {
            if (__instance.GetRootParent().gameObject.name != "__LIGHTMAPPED_PREFAB__")
            {
                Log.Warn($"[PlayerCinematicController_Start_Patch] - No NitroxEntity for \"{__instance.gameObject.GetFullHierarchyPath()}\" found!");
            }
        }
        // Beds and benches are synced by their own packets (bed animations, BenchChanged). Registering them here as
        // well would play the same animation twice on other players, which is how benches broke upstream.
        else if (!component.gameObject.GetComponent<Bed>() && !__instance.GetComponentInParent<Bench>())
        {
            component.gameObject.EnsureComponent<MultiplayerCinematicReference>().AddController(__instance);
        }
    }
}
