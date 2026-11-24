using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.Spawning;
using NitroxClient.GameLogic.Spawning.Metadata.Processor;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Applies metadata to a spawned GrownPlant when it was provided by <see cref="InventoryItemEntitySpawner"/>
/// </summary>
public sealed partial class GrowingPlant_SpawnGrownModelAsync_Patch : NitroxPatch, IDynamicPatch
{
    internal static readonly MethodInfo TARGET_METHOD = AccessTools.EnumeratorMoveNext(Reflect.Method((GrowingPlant t) => t.SpawnGrownModelAsync()));

    /*
     * REPLACE:
     * grownPlant.SendMessage("OnGrown", SendMessageOptions.DontRequireReceiver);
     * BY:
     * GrowingPlant_SpawnGrownModelAsync_Patch.OnGrown(grownPlant, this);
     */
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions).MatchStartForward([
                                                new CodeMatch(OpCodes.Ldstr, "OnGrown"),
                                                new CodeMatch(OpCodes.Ldc_I4_1),
                                                new CodeMatch(OpCodes.Callvirt)
                                            ])
                                            .RemoveInstructions(3) // Remove the Ldstr, Ldc_I4_1 and callvirt
                                            .Insert([ // GrownPlant component is already on stack
                                                new CodeInstruction(OpCodes.Ldloc_1), // Ldloc_1 refers to this instance (GrowingPlant)
                                                new CodeInstruction(OpCodes.Call, Reflect.Method(() => OnGrown(default, default)))
                                            ])
                                            .InstructionEnumeration();
    }

    public static void OnGrown(GrownPlant grownPlant, GrowingPlant growingPlant)
    {
        bool hasMetadata = growingPlant.TryGetReference(out PlantableMetadata metadata);
        if (grownPlant.TryGetComponent(out FruitPlant fruitPlant) && hasMetadata && metadata.FruitPlantMetadata != null)
        {
            // Only useful stuff from FruitPlant.OnGrown, whose picked states and timer the metadata replaces
            fruitPlant.Initialize();
            fruitPlant.fruitSpawnEnabled = true;
        }
        else
        {
            // Original call if we don't need to apply anything
            grownPlant.SendMessage("OnGrown", SendMessageOptions.DontRequireReceiver);
        }

        // After OnGrown, which would make the FruitPlant added here grow its parts back
        PickablePlants.Track(grownPlant.gameObject);

        if (hasMetadata && grownPlant.seed)
        {
            Resolve<PlantableMetadataProcessor>().ApplyToGrownPlant(grownPlant.seed, metadata);
        }
    }
}
