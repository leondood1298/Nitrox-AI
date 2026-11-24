using System.Reflection;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Broadcasts crafting start
/// </summary>
public sealed partial class GhostCrafter_OnCraftingBegin_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((GhostCrafter t) => t.OnCraftingBegin(default(TechType), default(float)));

    public static void Postfix(GhostCrafter __instance, TechType techType, float duration)
    {
        // We favor targeting the CrafterLogic instead of the GhostCrafter because in the base upgrade console module, the NitroxEntity is
        // on the CrafterLogic only. On every other crafter type, both CrafterLogic and GhostCrafter are on the same object.

        // Also for base upgrade console module, crafterLogic is nullified and never updated, so we use _logic instead for every crafter
        if (__instance._logic.TryGetIdOrWarn(out NitroxId crafterLogicId))
        {
            // The craft only began because this game holds the fabricator's exclusive lock (see Fabricators.MayCraft), which
            // it keeps until the craft is done
            Resolve<Entities>().BroadcastMetadataUpdate(crafterLogicId, new CrafterMetadata(techType.ToDto(), __instance._logic.timeCraftingBegin, duration, __instance._logic.numCrafted, __instance._logic.linkedIndex));
        }
    }
}
