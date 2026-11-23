using System.Collections.Generic;
using System.Reflection;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class BaseBioReactor_ProducePower_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((BaseBioReactor t) => t.ProducePower(0f));

    public static bool Prefix(BaseBioReactor __instance, out List<NitroxId> __state)
    {
        __state = CollectItemIds(__instance);
        return !__instance.TryGetNitroxId(out NitroxId id) || Resolve<SimulationOwnership>().HasAnyLockType(id);
    }

    public static void Postfix(BaseBioReactor __instance, List<NitroxId> __state)
    {
        if (__state.Count == 0 || !__instance.TryGetNitroxId(out NitroxId nitroxId) || !Resolve<SimulationOwnership>().HasAnyLockType(nitroxId))
        {
            return;
        }
        HashSet<NitroxId> remainingIds = new(CollectItemIds(__instance));
        foreach (NitroxId consumedId in __state)
        {
            if (!remainingIds.Contains(consumedId))
            {
                Resolve<Entities>().RemoveEntity(consumedId);
                Resolve<IPacketSender>().Send(new EntityDestroyed(consumedId));
            }
        }
    }

    private static List<NitroxId> CollectItemIds(BaseBioReactor reactor)
    {
        List<NitroxId> itemIds = new();
        if (reactor.container != null)
        {
            foreach (InventoryItem inventoryItem in reactor.container)
            {
                if (inventoryItem != null && inventoryItem.item && inventoryItem.item.TryGetNitroxId(out NitroxId nitroxId))
                {
                    itemIds.Add(nitroxId);
                }
            }
        }
        return itemIds;
    }
}
