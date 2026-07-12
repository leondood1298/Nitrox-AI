using System;
using System.CodeDom.Compiler;
using System.Reflection;
using HarmonyLib;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Helper;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.Simulation;
using NitroxClient.MonoBehaviours;

namespace NitroxPatcher.Patches.Dynamic;

public sealed class CollectShiny_TryPickupShinyTarget_Patch : NitroxPatch, IDynamicPatch, INitroxPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((CollectShiny t) => t.TryPickupShinyTarget());
    private static bool applyingGrantedPickup;

    public static bool Prefix(CollectShiny __instance)
    {
        if (applyingGrantedPickup || !__instance.shinyTarget || !__instance.shinyTarget.TryGetComponent(out MapRoomCamera camera) || !camera.TryGetNitroxId(out NitroxId cameraId))
        {
            return true;
        }

        SimulationOwnership ownership = Resolve<SimulationOwnership>();
        if (ownership.HasExclusiveLock(cameraId))
        {
            EntityPositionBroadcaster.WatchEntity(cameraId);
            return true;
        }

        StalkerCameraGrab context = new(__instance, camera.gameObject);
        ownership.RequestSimulationLock(new LockRequest<StalkerCameraGrab>(cameraId, SimulationLockType.EXCLUSIVE, OnLockResponse, context));
        return false;
    }

    private static void OnLockResponse(NitroxId cameraId, bool acquired, StalkerCameraGrab context)
    {
        if (!acquired || !context.CollectShiny || !context.Camera || context.CollectShiny.shinyTarget != context.Camera)
        {
            if (context.CollectShiny && context.CollectShiny.shinyTarget == context.Camera)
            {
                context.CollectShiny.shinyTarget = null;
                context.CollectShiny.targetPickedUp = false;
            }
            return;
        }

        EntityPositionBroadcaster.WatchEntity(cameraId);
        try
        {
            applyingGrantedPickup = true;
            context.CollectShiny.TryPickupShinyTarget();
        }
        finally
        {
            applyingGrantedPickup = false;
        }
    }

    [GeneratedCode("Nitrox.Analyzers", "1.0.13.0")]
    public override void Patch(Harmony harmony)
    {
        PatchMultiple(harmony, TARGET_METHOD, new Func<CollectShiny, bool>(Prefix).Method);
    }
}
