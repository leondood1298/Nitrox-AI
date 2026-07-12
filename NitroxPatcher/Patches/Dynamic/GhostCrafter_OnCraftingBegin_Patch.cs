using System.Reflection;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.Spawning.Metadata.Processor;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Broadcasts crafting start and claims exclusive ownership on the crafter
/// </summary>
public sealed partial class GhostCrafter_OnCraftingBegin_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((GhostCrafter t) => t.OnCraftingBegin(default(TechType), default(float)));

    public static void Postfix(GhostCrafter __instance, TechType techType, float duration)
    {
        // We favor targeting the CrafterLogic instead of the GhostCrafter because in the base upgrade console module, the NitroxEntity is
        // on the CrafterLogic only. On every other crafter type, both CrafterLogic and GhostCrafter are on the same object.

        // Also for base upgrade console module, crafterLogic is nullified and never updated, so we use _logic instead for every crafter
        if (CrafterIdentity.TryGetId(__instance._logic, out NitroxId crafterLogicId))
        {
            float startTime = DayNightCycle.main.timePassedAsFloat;
            Resolve<SimulationOwnership>().RequestSimulationLock(crafterLogicId, SimulationLockType.EXCLUSIVE);
            Resolve<Entities>().BroadcastMetadataUpdate(crafterLogicId, new CrafterMetadata(techType.ToDto(), startTime, duration, __instance._logic.numCrafted, __instance._logic.linkedIndex));
            CrafterMetadataProcessor.MarkLocalCraftAccounted(crafterLogicId, startTime);
        }
    }
}
