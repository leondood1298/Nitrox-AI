using System.Reflection;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class Pickupable_Pickup_Patch : NitroxPatch, IDynamicPatch
{
    public sealed record PickupScanTarget(NitroxId ResourceId, TechType TechType, Vector3 Position);

    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((Pickupable p) => p.Pickup(default));

    public static void Prefix(Pickupable __instance, out PickupScanTarget? __state)
    {
        __state = __instance.TryGetNitroxId(out NitroxId resourceId)
            ? new PickupScanTarget(resourceId, __instance.GetTechType(), __instance.transform.position)
            : null;
        Resolve<Items>().PickedUpByPlayer(__instance.gameObject, __instance.GetTechType());
    }

    public static void Postfix(PickupScanTarget? __state)
    {
        if (__state != null)
        {
            MapRoomScanResults.RemoveLocalResource(__state.ResourceId, __state.TechType, __state.Position);
        }
    }
}

