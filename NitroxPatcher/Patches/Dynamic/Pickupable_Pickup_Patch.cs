using System.Reflection;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class Pickupable_Pickup_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((Pickupable p) => p.Pickup(default));

    public static void Prefix(Pickupable __instance)
    {
        Resolve<Items>().PickedUpByPlayer(__instance.gameObject, __instance.GetTechType());
    }

    public static void Postfix(Pickupable __instance)
    {
        if (__instance.TryGetNitroxId(out NitroxId resourceId))
        {
            MapRoomScanResults.RemoveLocalResource(resourceId);
        }
    }
}

