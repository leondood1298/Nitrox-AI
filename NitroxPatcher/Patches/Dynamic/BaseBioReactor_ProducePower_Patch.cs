using System;
using System.Collections.Generic;
using System.Reflection;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Helper;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.Extensions;
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
        HashSet<NitroxId> hashSet = new HashSet<NitroxId>(CollectItemIds(__instance));
        foreach (NitroxId item in __state)
        {
            if (!hashSet.Contains(item))
            {
                Resolve<Entities>().RemoveEntity(item);
                Resolve<IPacketSender>().Send(new EntityDestroyed(item));
            }
        }
    }

    private static List<NitroxId> CollectItemIds(BaseBioReactor reactor)
    {
        List<NitroxId> list = new List<NitroxId>();
        if (reactor.container != null)
        {
            foreach (InventoryItem item in (IEnumerable<InventoryItem>)reactor.container)
            {
                if (item != null && item.item && item.item.TryGetNitroxId(out NitroxId nitroxId))
                {
                    list.Add(nitroxId);
                }
            }
        }
        return list;
    }
}
