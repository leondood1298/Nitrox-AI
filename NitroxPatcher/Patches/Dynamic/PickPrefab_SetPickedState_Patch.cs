using System.Reflection;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Sync destruction of giver object when required (a melon is gone once it is picked).
///     This code is only executed from a local input so we don't need to check for ownership.
/// </summary>
public sealed partial class PickPrefab_SetPickedState_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PickPrefab t) => t.SetPickedState(default));

    public static void Postfix(PickPrefab __instance, bool newPickedState)
    {
        if (newPickedState && __instance.destroyOnPicked)
        {
            BroadcastDeletion(__instance);
        }
    }

    private static void BroadcastDeletion(PickPrefab pickPrefab)
    {
        if (pickPrefab.TryGetNitroxId(out NitroxId objectId) ||
            (pickPrefab.TryGetComponent(out GrownPlant grownPlant) && grownPlant.seed && grownPlant.seed.TryGetNitroxId(out objectId)))
        {
            Resolve<IPacketSender>().Send(new EntityDestroyed(objectId));
        }
    }
}
