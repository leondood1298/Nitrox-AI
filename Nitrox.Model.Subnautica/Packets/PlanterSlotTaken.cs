using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     From the server to one player: the slot of a planter that this player's game planted a seed in was taken by
///     another player's seed a moment before. The game moves its seed to a free slot, or gives it back to its player
///     when the planter is full.
/// </summary>
[Serializable]
public class PlanterSlotTaken : Packet
{
    public NitroxId PlantableId { get; }
    public int SlotId { get; }

    public PlanterSlotTaken(NitroxId plantableId, int slotId)
    {
        PlantableId = plantableId;
        SlotId = slotId;
    }
}
