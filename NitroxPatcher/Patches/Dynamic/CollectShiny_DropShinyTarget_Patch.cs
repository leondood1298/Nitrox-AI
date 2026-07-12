using System;
using System.CodeDom.Compiler;
using System.Reflection;
using HarmonyLib;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Helper;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

public sealed class CollectShiny_DropShinyTarget_Patch : NitroxPatch, IDynamicPatch, INitroxPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((CollectShiny t) => t.DropShinyTarget(default(GameObject)));

    public static void Prefix(GameObject target)
    {
        if (target && target.TryGetComponent(out MapRoomCamera camera) && camera.TryGetNitroxId(out NitroxId cameraId) && Resolve<SimulationOwnership>().HasExclusiveLock(cameraId))
        {
            Resolve<SimulationOwnership>().RequestSimulationLock(cameraId, SimulationLockType.TRANSIENT);
        }
    }

    [GeneratedCode("Nitrox.Analyzers", "1.0.13.0")]
    public override void Patch(Harmony harmony)
    {
        PatchMultiple(harmony, TARGET_METHOD, new Action<GameObject>(Prefix).Method);
    }
}
