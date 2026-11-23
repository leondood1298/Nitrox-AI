using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class ResourceTrackerDatabase_Register_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method(() => ResourceTrackerDatabase.Register(string.Empty, Vector3.zero, TechType.None));
    private const float MOVED_SQR_DISTANCE_THRESHOLD = 0.0001f;

    public static void Prefix(string uniqueId, TechType resourceTechType, out Vector3? __state)
    {
        ResourceTrackerDatabase.ResourceInfo existing = FindNode(uniqueId, resourceTechType);
        __state = existing != null ? existing.position : null;
    }

    public static void Postfix(string uniqueId, Vector3 resourcePosition, TechType resourceTechType, Vector3? __state)
    {
        if (!__state.HasValue || (__state.Value - resourcePosition).sqrMagnitude < MOVED_SQR_DISTANCE_THRESHOLD)
        {
            return;
        }
        ResourceTrackerDatabase.ResourceInfo moved = FindNode(uniqueId, resourceTechType);
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

    private static ResourceTrackerDatabase.ResourceInfo FindNode(string uniqueId, TechType techType)
    {
        foreach (ResourceTrackerDatabase.ResourceInfo info in ResourceTrackerDatabase.GetNodes(techType))
        {
            if (info.uniqueId == uniqueId)
            {
                return info;
            }
        }
        return null;
    }
}
