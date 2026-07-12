using System;
using System.CodeDom.Compiler;
using System.Reflection;
using HarmonyLib;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Helper;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Prevents every connected client from independently draining Scanner Room power.
/// Visual scanning and blip updates still run locally; only the simulation owner may let the drain timer elapse.
/// </summary>
public sealed class MapRoomFunctionality_UpdateScanning_Patch : NitroxPatch, IDynamicPatch, INitroxPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomFunctionality t) => t.UpdateScanning());

    public static void Prefix(MapRoomFunctionality __instance)
    {
        bool hasEntityId = __instance.TryGetNitroxId(out NitroxId roomId);
        bool hasOwnership = hasEntityId && Resolve<SimulationOwnership>().HasAnyLockType(roomId);
        if (ShouldSuppressPowerDrain(hasEntityId, hasOwnership))
        {
            __instance.timeLastPowerDrain = Time.time;
        }
    }

    public static bool ShouldSuppressPowerDrain(bool hasEntityId, bool hasOwnership) => hasEntityId && !hasOwnership;

    [GeneratedCode("Nitrox.Analyzers", "1.0.13.0")]
    public override void Patch(Harmony harmony)
    {
        PatchMultiple(harmony, TARGET_METHOD, new Action<MapRoomFunctionality>(Prefix).Method);
    }
}
