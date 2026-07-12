using System;
using System.CodeDom.Compiler;
using System.Reflection;
using HarmonyLib;
using Nitrox.Model.Helper;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

public sealed class ResourceTrackerDatabase_Register_Patch : NitroxPatch, IDynamicPatch, INitroxPatch
{
    private delegate void PrefixDelegate(string uniqueId, TechType resourceTechType, out Vector3? __state);

    private static readonly MethodInfo TARGET_METHOD = Reflect.Method(() => ResourceTrackerDatabase.Register(string.Empty, Vector3.zero, TechType.None));

    public static void Prefix(string uniqueId, TechType resourceTechType, out Vector3? __state)
    {
        __state = null;
        foreach (ResourceTrackerDatabase.ResourceInfo info in ResourceTrackerDatabase.GetNodes(resourceTechType))
        {
            if (info.uniqueId == uniqueId)
            {
                __state = info.position;
                return;
            }
        }
    }

    public static void Postfix(string uniqueId, Vector3 resourcePosition, TechType resourceTechType, Vector3? __state)
    {
        if (!__state.HasValue || (__state.Value - resourcePosition).sqrMagnitude < 0.0001f)
        {
            return;
        }
        ResourceTrackerDatabase.ResourceInfo moved = null;
        foreach (ResourceTrackerDatabase.ResourceInfo info in ResourceTrackerDatabase.GetNodes(resourceTechType))
        {
            if (info.uniqueId == uniqueId)
            {
                moved = info;
                break;
            }
        }
        if (moved == null)
        {
            return;
        }
        MapRoomScanResultBroadcaster broadcaster = Resolve<MapRoomScanResultBroadcaster>();
        foreach (MapRoomFunctionality mapRoom in MapRoomFunctionality.mapRooms)
        {
            broadcaster.BroadcastMoved(mapRoom, moved);
        }
    }

    [GeneratedCode("Nitrox.Analyzers", "1.0.13.0")]
    public override void Patch(Harmony harmony)
    {
        PatchMultiple(harmony, TARGET_METHOD, new PrefixDelegate(Prefix).Method, new Action<string, Vector3, TechType, Vector3?>(Postfix).Method);
    }
}
