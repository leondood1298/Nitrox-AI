using System.Collections.Generic;
using Nitrox.Model.DataStructures;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public static class CrafterPowerAccounting
{
    private static readonly Dictionary<NitroxId, float> lastConsumedCraftStartById = [];

    public static bool TryAccount(NitroxId? crafterId, float startTime, bool initialSyncCompleted)
    {
        if (crafterId != null)
        {
            if (lastConsumedCraftStartById.TryGetValue(crafterId, out float previousStart) && previousStart == startTime)
            {
                return false;
            }
            lastConsumedCraftStartById[crafterId] = startTime;
        }
        return initialSyncCompleted;
    }

    public static void MarkAccounted(NitroxId crafterId, float startTime)
    {
        if (crafterId != null)
        {
            lastConsumedCraftStartById[crafterId] = startTime;
        }
    }
}
