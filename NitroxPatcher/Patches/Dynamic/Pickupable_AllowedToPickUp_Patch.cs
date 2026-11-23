using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     An item another player holds exclusively (in a propulsion cannon, or in the mouth of a Stalker they simulate) cannot
///     be taken by hand, see <see cref="SimulationOwnership.MayBeTakenByHand" />.
/// </summary>
public sealed partial class Pickupable_AllowedToPickUp_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Pickupable t) => t.AllowedToPickUp());

    public static void Postfix(Pickupable __instance, ref bool __result)
    {
        __result = __result && Resolve<SimulationOwnership>().MayBeTakenByHand(__instance.gameObject);
    }
}
