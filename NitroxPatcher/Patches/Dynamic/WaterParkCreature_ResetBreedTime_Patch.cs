using System.Reflection;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Syncs a fish's breeding time. Every game breeds the fish of a containment and may choose another partner; only the
///     game of the player who simulates the containment makes the newborn (WaterParkCreature_BornAsync_Patch), so its
///     breeding times are the ones sent. A fish outside any containment is reported by its own simulating player.
/// </summary>
public sealed partial class WaterParkCreature_ResetBreedTime_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((WaterParkCreature t) => t.ResetBreedTime());

    public static void Postfix(WaterParkCreature __instance)
    {
        if (!__instance.TryGetNitroxId(out NitroxId creatureId))
        {
            return;
        }

        SimulationOwnership simulationOwnership = Resolve<SimulationOwnership>();
        bool decidesHere = __instance.currentWaterPark.TryGetNitroxId(out NitroxId waterParkId) ?
                               ContainmentRules.DecidesHere(true, simulationOwnership.HasAnyLockType(waterParkId)) :
                               simulationOwnership.HasAnyLockType(creatureId);
        if (!decidesHere)
        {
            return;
        }

        Optional<WaterParkCreatureMetadata> metadata = Resolve<WaterParkCreatureMetadataExtractor>().Extract(__instance);
        if (metadata.HasValue)
        {
            Resolve<Entities>().BroadcastMetadataUpdate(creatureId, metadata.Value);
        }
    }
}
