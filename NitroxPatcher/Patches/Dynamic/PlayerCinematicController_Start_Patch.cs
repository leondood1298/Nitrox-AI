using System;
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
        else if (!component.gameObject.GetComponent<Bed>())
        {
            component.gameObject.EnsureComponent<MultiplayerCinematicReference>().AddController(__instance);
        }
    }
}
