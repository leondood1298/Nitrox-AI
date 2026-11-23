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

    /// <summary>
    ///     Decides whether applying a crafter update should charge base power on this client. Only the update that
    ///     starts a craft carries a duration. Pickup updates are sent with none, and they carry the picker's own
    ///     local start time, so matching on start time alone charged a multi-item craft again on every other client
    ///     whenever someone other than the crafter took the first item.
    /// </summary>
    public static bool ShouldConsume(NitroxId? crafterId, float startTime, float duration, bool initialSyncCompleted)
    {
        return duration > 0f && TryAccount(crafterId, startTime, initialSyncCompleted);
    }

    public static void MarkAccounted(NitroxId crafterId, float startTime)
    {
        if (crafterId != null)
        {
            lastConsumedCraftStartById[crafterId] = startTime;
        }
    }
}
