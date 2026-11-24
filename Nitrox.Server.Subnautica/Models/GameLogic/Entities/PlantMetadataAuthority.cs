using System;
using System.Collections.Generic;
using System.Globalization;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <summary>
///     Merges what players report about a plant: its picked fruit (or seeds, samples), its health and its slot. Every game
///     reports the whole plant, so a report can carry a fruit as it was before another player picked it or before the
///     plant grew it back. Taken as sent, a picked fruit came back to be picked again, or a fruit grown back vanished.
///     <list type="bullet">
///         <item>Anyone may pick a fruit: a fruit picked in any report stays picked.</item>
///         <item>
///             Only the player who decides the plant (who simulates its planter's base, containment or outdoor planter)
///             grows fruit back, and not a fruit picked moments ago: that player's game had not heard of the pick yet.
///         </item>
///         <item>The plant's health only goes down, except when the deciding player reports it.</item>
///         <item>The slot of a planted plant is the one it was planted in (see EntitySpawnedByClientProcessor).</item>
///     </list>
/// </summary>
internal static class PlantMetadataAuthority
{
    /// <param name="pickedLately">Whether the fruit at an index was picked moments ago.</param>
    /// <returns>Null when the report cannot be merged (another number of fruit).</returns>
    public static FruitPlantMetadata? MergeFruit(FruitPlantMetadata? stored, FruitPlantMetadata? requested, bool senderDecides, Func<int, bool> pickedLately)
    {
        if (requested == null)
        {
            return stored;
        }
        if (stored?.PickedStates == null || requested.PickedStates == null)
        {
            return requested;
        }
        if (stored.PickedStates.Length != requested.PickedStates.Length)
        {
            return null;
        }

        bool[] merged = new bool[stored.PickedStates.Length];
        for (int i = 0; i < merged.Length; i++)
        {
            bool grownBack = stored.PickedStates[i] && !requested.PickedStates[i];
            merged[i] = grownBack ? !(senderDecides && !pickedLately(i)) : stored.PickedStates[i] || requested.PickedStates[i];
        }
        // The deciding player's timer is the one that grows fruit back; a pick of another player can only start it later
        float timeNextFruit = senderDecides ? requested.TimeNextFruit : Math.Max(stored.TimeNextFruit, requested.TimeNextFruit);
        return new FruitPlantMetadata(merged, timeNextFruit);
    }

    /// <inheritdoc cref="MergeFruit" />
    public static PlantableMetadata? MergePlantable(PlantableMetadata? stored, PlantableMetadata requested, bool senderDecides, Func<int, bool> pickedLately)
    {
        if (stored == null)
        {
            return requested;
        }
        FruitPlantMetadata? fruit = MergeFruit(stored.FruitPlantMetadata, requested.FruitPlantMetadata, senderDecides, pickedLately);
        if (fruit == null && (stored.FruitPlantMetadata != null || requested.FruitPlantMetadata != null))
        {
            return null;
        }
        float timeStartGrowth = stored.TimeStartGrowth != -1f ? stored.TimeStartGrowth : requested.TimeStartGrowth;
        return new PlantableMetadata(timeStartGrowth, stored.SlotID, fruit, MergeHealth(stored.Health, requested.Health, senderDecides));
    }

    /// <summary>
    ///     A health of zero or less is no report: a plant's death is its removal.
    /// </summary>
    public static float MergeHealth(float stored, float requested, bool senderDecides)
    {
        if (requested <= 0f)
        {
            return stored;
        }
        if (senderDecides || stored <= 0f)
        {
            return requested;
        }
        return Math.Min(stored, requested);
    }

    /// <returns>The fruit that <paramref name="accepted" /> has picked and <paramref name="stored" /> did not.</returns>
    public static IEnumerable<int> NewlyPicked(FruitPlantMetadata? stored, FruitPlantMetadata? accepted)
    {
        if (accepted?.PickedStates == null)
        {
            yield break;
        }
        for (int i = 0; i < accepted.PickedStates.Length; i++)
        {
            bool wasPicked = stored?.PickedStates != null && i < stored.PickedStates.Length && stored.PickedStates[i];
            if (accepted.PickedStates[i] && !wasPicked)
            {
                yield return i;
            }
        }
    }

    /// <returns>
    ///     The fruit <paramref name="requested" /> has on the plant that <paramref name="accepted" /> keeps picked: a fruit
    ///     the sender's game grew (or still showed) that the merge refused.
    /// </returns>
    public static IEnumerable<int> RefusedGrowths(FruitPlantMetadata? requested, FruitPlantMetadata? accepted)
    {
        if (requested?.PickedStates == null || accepted?.PickedStates == null)
        {
            yield break;
        }
        for (int i = 0; i < requested.PickedStates.Length && i < accepted.PickedStates.Length; i++)
        {
            if (!requested.PickedStates[i] && accepted.PickedStates[i])
            {
                yield return i;
            }
        }
    }

    /// <summary>
    ///     One line for the plant diagnostics: which fruit each state has on the plant, and when the next one grows.
    /// </summary>
    public static string Describe(FruitPlantMetadata? fruit)
    {
        if (fruit?.PickedStates == null)
        {
            return "-";
        }
        return $"[{string.Join(",", FruitPlantMetadata.PresentIndices(fruit.PickedStates))}]/{fruit.PickedStates.Length}@{fruit.TimeNextFruit.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <returns>True when the sender's copy differs from the merged state, so it must be given the merged one.</returns>
    public static bool SenderIsBehind(FruitPlantMetadata? requested, FruitPlantMetadata? accepted)
    {
        if (requested?.PickedStates == null || accepted?.PickedStates == null)
        {
            return false;
        }
        for (int i = 0; i < requested.PickedStates.Length && i < accepted.PickedStates.Length; i++)
        {
            if (requested.PickedStates[i] != accepted.PickedStates[i])
            {
                return true;
            }
        }
        return requested.TimeNextFruit != accepted.TimeNextFruit;
    }

    /// <inheritdoc cref="SenderIsBehind(FruitPlantMetadata, FruitPlantMetadata)" />
    public static bool SenderIsBehind(PlantableMetadata requested, PlantableMetadata accepted)
    {
        return SenderIsBehind(requested.FruitPlantMetadata, accepted.FruitPlantMetadata) ||
               (requested.Health > 0f && accepted.Health > 0f && requested.Health != accepted.Health) ||
               requested.SlotID != accepted.SlotID;
    }
}
