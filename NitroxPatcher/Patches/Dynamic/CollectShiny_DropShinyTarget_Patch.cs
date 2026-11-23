using System.Reflection;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class CollectShiny_DropShinyTarget_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((CollectShiny t) => t.DropShinyTarget(default(GameObject)));

    public static void Prefix(GameObject target)
    {
        if (target && target.TryGetComponent(out MapRoomCamera camera) && camera.TryGetNitroxId(out NitroxId cameraId))
        {
            SimulationOwnership ownership = Resolve<SimulationOwnership>();
            MapRoomCameras mapRoomCameras = Resolve<MapRoomCameras>();
            if (Resolve<StalkerCameraLockPurposeTracker>().TryConsumeForDowngrade(cameraId,
                    ownership.HasExclusiveLock(cameraId),
                    mapRoomCameras.HasPendingControl(cameraId) || mapRoomCameras.HasLocalControl(cameraId)))
            {
                ownership.RequestSimulationLock(cameraId, SimulationLockType.TRANSIENT);
            }
        }
    }
}
