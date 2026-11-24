using System.Collections;
using System.Reflection;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class ExosuitClawArm_OnPickupAsync_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((ExosuitClawArm t) => t.OnPickupAsync(default, default, default));

    public static IEnumerator Postfix(IEnumerator __result, ExosuitClawArm __instance)
    {
        if (__instance.exosuit.TryGetIdOrWarn(out NitroxId id))
        {
            Pickupable pickupable = __instance.exosuit.GetActiveTarget().GetComponent<Pickupable>();

            // A fruit or sample (PickPrefab) is no item itself: the item the game makes for it is reported by PickPrefab_AddToContainerAsync_Patch
            if (pickupable != null && pickupable.isPickupable && __instance.exosuit.storageContainer.container.HasRoomFor(pickupable))
            {
                Resolve<Items>().PickedUp(pickupable.gameObject, pickupable.GetTechType(), id);
            }
        }

        Resolve<Items>().PickingUpCount++;

        try
        {
            while (__result.MoveNext())
            {
                yield return __result.Current;
            }
        }
        finally
        {
            Resolve<Items>().PickingUpCount--;
        }
    }
}
