using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Shares how far a base is flooded, so that a player who joins later, or everybody after a restart, does not see the
///     water start again from nothing. Every game floods its copy of the base from the leaks; the player who simulates the
///     base (who also deals its crush damage) reports the level now and then, when it has changed.
/// </summary>
public class BaseFloodBroadcaster(SimulationOwnership simulationOwnership, Entities entities, BaseFloodMetadataExtractor extractor)
{
    private const float SEND_INTERVAL_SECONDS = 2f;

    /// <summary>
    ///     Total water (summed over the cells, each from 0 to 1) a report has to differ by from the last one.
    /// </summary>
    private const float MIN_WATER_CHANGE = 0.05f;

    private readonly SimulationOwnership simulationOwnership = simulationOwnership;
    private readonly Entities entities = entities;
    private readonly BaseFloodMetadataExtractor extractor = extractor;
    private readonly Dictionary<NitroxId, float> lastSentWaterByBaseId = [];
    private readonly Dictionary<NitroxId, float> nextCheckTimeByBaseId = [];

    public void OnFloodSimUpdated(BaseFloodSim floodSim)
    {
        if (!floodSim.IsInitialized() || !floodSim.baseComp || !floodSim.baseComp.TryGetNitroxId(out NitroxId baseId) || !simulationOwnership.HasAnyLockType(baseId))
        {
            return;
        }
        float now = Time.time;
        if (nextCheckTimeByBaseId.TryGetValue(baseId, out float nextCheckTime) && now < nextCheckTime)
        {
            return;
        }
        nextCheckTimeByBaseId[baseId] = now + SEND_INTERVAL_SECONDS;

        BaseFloodMetadata metadata = extractor.Extract(floodSim);
        float water = TotalWater(metadata.FlatValueGrid);
        float? lastSent = lastSentWaterByBaseId.TryGetValue(baseId, out float sent) ? sent : null;
        if (!ShouldSend(lastSent, water))
        {
            return;
        }
        lastSentWaterByBaseId[baseId] = water;
        entities.BroadcastMetadataUpdate(baseId, metadata);
    }

    /// <summary>
    ///     A dry base is not reported until it has taken in water; after that each noticeable change is, draining included.
    /// </summary>
    internal static bool ShouldSend(float? lastSentWater, float water)
    {
        if (!lastSentWater.HasValue)
        {
            return water >= MIN_WATER_CHANGE;
        }
        return System.Math.Abs(water - lastSentWater.Value) >= MIN_WATER_CHANGE || (water == 0f && lastSentWater.Value != 0f);
    }

    internal static float TotalWater(float[]? grid)
    {
        float total = 0f;
        if (grid == null)
        {
            return total;
        }
        foreach (float value in grid)
        {
            total += value;
        }
        return total;
    }
}
