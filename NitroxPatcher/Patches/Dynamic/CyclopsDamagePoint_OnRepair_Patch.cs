using System.Reflection;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// <see cref="CyclopsExternalDamageManager.RepairPoint(CyclopsDamagePoint)"/> would seem like the correct method to patch, but adding to its postfix will
/// execute before <see cref="CyclopsDamagePoint.OnRepair"/> is finished working. Both owners and non-owners will be able to repair damage points on a ship.
/// </summary>
public sealed partial class CyclopsDamagePoint_OnRepair_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((CyclopsDamagePoint t) => t.OnRepair());

    public static void Postfix(CyclopsDamagePoint __instance)
    {
        // If the amount is high enough, it'll heal full
        SubRoot subRoot = __instance.GetComponentInParent<SubRoot>();
        Resolve<Cyclops>().OnDamagePointRepaired(subRoot, __instance, 999);

        // The repair packet is only relayed. Once the leak is gone, the player simulating the Cyclops updates its
        // stored state so that players who join later do not get the leak back.
        if (subRoot && subRoot.TryGetNitroxId(out NitroxId cyclopsId) && Resolve<SimulationOwnership>().HasAnyLockType(cyclopsId))
        {
            Resolve<Cyclops>().BroadcastMetadataChange(cyclopsId);
        }
    }
}
