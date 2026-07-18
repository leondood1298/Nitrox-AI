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

        MapRoomCameras mapRoomCameras = Resolve<MapRoomCameras>();
        if (mapRoomCameras.HasPendingControl(cameraId))
        {
            return false;
        }
        if (mapRoomCameras.HasLocalControl(cameraId))
        {
            return true;
        }

        SimulationOwnership ownership = Resolve<SimulationOwnership>();
        if (ownership.HasExclusiveLock(cameraId))
        {
            EntityPositionBroadcaster.WatchEntity(cameraId);
            return true;
        }

        Resolve<StalkerCameraLockPurposeTracker>().Forget(cameraId);
        StalkerCameraGrab context = new(__instance, camera.gameObject);
        ownership.RequestSimulationLock(new LockRequest<StalkerCameraGrab>(cameraId, SimulationLockType.EXCLUSIVE, OnLockResponse, context));
        return false;
    }

    private static void OnLockResponse(NitroxId cameraId, bool acquired, StalkerCameraGrab context)
    {
        StalkerCameraLockPurposeTracker purposeTracker = Resolve<StalkerCameraLockPurposeTracker>();
        if (!acquired)
        {
            purposeTracker.Forget(cameraId);
            InvalidateContext(context);
            return;
        }

        MapRoomCameras mapRoomCameras = Resolve<MapRoomCameras>();
        purposeTracker.RecordAcquisition(cameraId, mapRoomCameras.HasLocalControl(cameraId));
        if (!context.CollectShiny || !context.Camera || context.CollectShiny.shinyTarget != context.Camera)
        {
            ReleaseStalkerAcquisition(cameraId, purposeTracker, mapRoomCameras);
            InvalidateContext(context);
            return;
        }

        EntityPositionBroadcaster.WatchEntity(cameraId);
        bool pickupApplied = false;
        try
        {
            applyingGrantedPickup = true;
            context.CollectShiny.TryPickupShinyTarget();
            pickupApplied = true;
        }
        finally
        {
            applyingGrantedPickup = false;
            if (!pickupApplied)
            {
                ReleaseStalkerAcquisition(cameraId, purposeTracker, mapRoomCameras);
            }
        }
    }

    private static void ReleaseStalkerAcquisition(NitroxId cameraId,
        StalkerCameraLockPurposeTracker purposeTracker, MapRoomCameras mapRoomCameras)
    {
        SimulationOwnership ownership = Resolve<SimulationOwnership>();
        if (purposeTracker.TryConsumeForDowngrade(cameraId, ownership.HasExclusiveLock(cameraId),
                mapRoomCameras.HasPendingControl(cameraId) || mapRoomCameras.HasLocalControl(cameraId)))
        {
            ownership.RequestSimulationLock(cameraId, SimulationLockType.TRANSIENT);
        }
    }

    private static void InvalidateContext(StalkerCameraGrab context)
    {
        if (context.CollectShiny && context.CollectShiny.shinyTarget == context.Camera)
        {
            context.CollectShiny.shinyTarget = null;
            context.CollectShiny.targetPickedUp = false;
        }
    }

    [GeneratedCode("Nitrox.Analyzers", "1.0.13.0")]
    public override void Patch(Harmony harmony)
    {
        PatchMultiple(harmony, TARGET_METHOD, new Func<CollectShiny, bool>(Prefix).Method);
    }
}
