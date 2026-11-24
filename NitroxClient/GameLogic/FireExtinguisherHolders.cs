using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using NitroxClient.MonoBehaviours;

namespace NitroxClient.GameLogic;

/// <summary>
///     The tank on a fire extinguisher holder is made new by every game whose player takes it
///     (FireExtinguisherHolder.TakeTankAsync). Two players who take it at the same moment each got one; now the tank names
///     the holder as its source, and the server gives it to the first (see <see cref="ItemSources" />).
/// </summary>
public class FireExtinguisherHolders
{
    private readonly ItemSources itemSources;

    /// <summary>
    ///     The extinguisher last put on each holder, as the holder's metadata carries it.
    /// </summary>
    private readonly Dictionary<NitroxId, NitroxId> storedTanks = [];

    public FireExtinguisherHolders(ItemSources itemSources)
    {
        this.itemSources = itemSources;
    }

    /// <summary>
    ///     The token of the tank on a holder. Each tank put on the holder gives a new one: a claim on the previous tank is
    ///     still remembered by the server for a while, and another player taking the next tank must not lose to it.
    /// </summary>
    internal static string TankToken(NitroxId? storedTankId) => storedTankId == null ? "tank" : $"tank:{storedTankId}";

    /// <summary>
    ///     Called before the local player clicks a holder.
    /// </summary>
    public void Clicking(FireExtinguisherHolder holder)
    {
        if (holder.hasTank && !holder.isTakeTankAsyncInProgress && holder.TryGetNitroxId(out NitroxId holderId))
        {
            itemSources.Expect(TechType.FireExtinguisher, holderId, TankToken(GetStoredTank(holderId)));
        }
    }

    public NitroxId? GetStoredTank(NitroxId holderId) => storedTanks.TryGetValue(holderId, out NitroxId tankId) ? tankId : null;

    public void SetStoredTank(NitroxId holderId, NitroxId? tankId)
    {
        if (tankId == null)
        {
            storedTanks.Remove(holderId);
            return;
        }
        storedTanks[holderId] = tankId;
    }
}
