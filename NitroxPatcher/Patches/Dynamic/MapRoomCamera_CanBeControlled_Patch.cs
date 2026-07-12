using System;
using System.Collections.Generic;
using System.CodeDom.Compiler;
using System.Reflection;
using HarmonyLib;
using Nitrox.Model.Helper;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed class MapRoomCamera_CanBeControlled_Patch : NitroxPatch, IDynamicPatch, INitroxPatch
{
    private delegate void PostfixDelegate(MapRoomCamera instance, ref bool result);
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomCamera t) => t.CanBeControlled(null));
    private static readonly HashSet<int> loggedUnavailableCameras = [];

    public static void Postfix(MapRoomCamera __instance, ref bool __result)
    {
        bool vanillaResult = __result;
        bool controllerStateAllows = Resolve<MapRoomCameras>().CanSelectForControl(__instance);
        __result &= controllerStateAllows;
        int instanceId = __instance.GetInstanceID();
        if (__result)
        {
            loggedUnavailableCameras.Remove(instanceId);
        }
        else if (loggedUnavailableCameras.Add(instanceId))
        {
            float energy = __instance.energyMixin ? __instance.energyMixin.charge : -1f;
            bool alive = __instance.liveMixin && __instance.liveMixin.IsAlive();
            bool attached = __instance.pickupAble && __instance.pickupAble.attached;
            Log.Warn($"[MapRoomCamera] Camera {__instance.cameraNumber} unavailable: vanilla={vanillaResult}, controllerState={controllerStateAllows}, energy={energy:F2}, alive={alive}, attached={attached}, activeAndEnabled={__instance.isActiveAndEnabled}");
        }
    }

    [GeneratedCode("Nitrox.Analyzers", "1.0.13.0")]
    public override void Patch(Harmony harmony)
    {
        PatchMultiple(harmony, TARGET_METHOD, null, new PostfixDelegate(Postfix).Method);
    }
}
