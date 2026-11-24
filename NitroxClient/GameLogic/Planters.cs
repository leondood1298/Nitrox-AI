using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.GameLogic.Helper;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Two players who plant in the same free slot at the same moment: the server keeps the first seed in it and tells the
///     game of the later one (<see cref="PlanterSlotTaken" />), which moves its seed to the next free slot, or gives it
///     back to its player when the planter is full.
/// </summary>
public class Planters(Items items)
{
    private readonly Items items = items;

    public void SlotTaken(NitroxId plantableId, int slotId)
    {
        if (!NitroxEntity.TryGetComponentFrom(plantableId, out Plantable plantable) || !plantable.currentPlanter || plantable.GetSlotID() != slotId)
        {
            return;
        }
        Planter planter = plantable.currentPlanter;
        if (!InventoryContainerHelper.TryGetOwnerId(planter.storageContainer.container.tr, out NitroxId planterId))
        {
            return;
        }

        // The seed still holds the slot here, so the planter gives another one
        int freeSlot = planter.GetFreeSlotID(plantable.size == Plantable.PlantSize.Large);
        if (freeSlot >= 0)
        {
            using (PacketSuppressor<EntityMetadataUpdate>.Suppress())
            {
                // By slot: the seed stays in the planter's container while it changes slot
                planter.RemoveItem(slotId);
                planter.AddItem(plantable, freeSlot);
            }
            items.MovedIntoInventory(plantable.gameObject, planterId);
            return;
        }

        Pickupable pickupable = plantable.pickupable;
        planter.RemoveItem(plantable);
        if (!Inventory.main.Pickup(pickupable))
        {
            pickupable.Drop(Player.main.transform.position + MainCamera.camera.transform.forward);
        }
        ErrorMessage.AddMessage(Language.main.Get("Nitrox_PlanterFull"));
    }

    /// <summary>
    ///     Whether a seed received from another player can take the slot it was planted in, here. It cannot while this
    ///     game's own seed still holds it: the server kept the other seed there, and this game's seed is about to move.
    /// </summary>
    public static bool CanTakeSlot(bool slotOccupied, bool heldBySameSeed) => !slotOccupied || heldBySameSeed;
}
