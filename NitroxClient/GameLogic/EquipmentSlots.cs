using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic.Spawning.Metadata;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

public class EquipmentSlots
{
    private readonly IPacketSender packetSender;
    private readonly Entities entities;
    private readonly EntityMetadataManager entityMetadataManager;

    public EquipmentSlots(IPacketSender packetSender, Entities entities, EntityMetadataManager entityMetadataManager)
    {
        this.packetSender = packetSender;
        this.entities = entities;
        this.entityMetadataManager = entityMetadataManager;
    }

    public void BroadcastEquip(Pickupable pickupable, GameObject owner, string slot)
    {
        if (!owner.TryGetIdOrWarn(out NitroxId ownerId))
        {
            return;
        }
        if (!pickupable.TryGetIdOrWarn(out NitroxId itemId))
        {
            return;
        }

        if (owner.TryGetComponent(out Player player))
        {
            ReportPlayerEquipment(player, ownerId);
        }
        else
        {
            string classId = pickupable.RequireComponent<PrefabIdentifier>().ClassId;
            NitroxTechType techType = pickupable.GetTechType().ToDto();
            EntityMetadata metadata = entityMetadataManager.Extract(pickupable.gameObject).OrNull();

            // A torpedo module brings its torpedoes along
            InstalledModuleEntity moduleEntity = new(slot, classId, itemId, techType, metadata, ownerId, [.. Items.GetStoredItems(pickupable.gameObject, itemId, entityMetadataManager)]);
            entities.MarkAsSpawned(moduleEntity);

            if (packetSender.Send(new EntitySpawnedByClient(moduleEntity, true)))
            {
                Log.Debug($"Sent: Added module {pickupable.GetTechType()} ({itemId}) to equipment {owner.GetFullHierarchyPath()} in slot {slot}");
            }
        }
    }

    public void BroadcastUnequip(Pickupable pickupable, GameObject owner)
    {
        if (!pickupable.TryGetIdOrWarn(out NitroxId itemId))
        {
            return;
        }
        if (!owner.TryGetIdOrWarn(out NitroxId ownerId))
        {
            return;
        }

        bool ownerIsPlayer = owner.TryGetComponent(out Player player);

        // Sync oxygen tank metadata when unequipped
        if (pickupable.TryGetComponent(out Oxygen oxygen) && (!ownerIsPlayer || InRunningGame))
        {
            entities.EntityMetadataChanged(oxygen, itemId);
        }

        if (ownerIsPlayer)
        {
            ReportPlayerEquipment(player, ownerId);
        }
        else
        {
            entities.RemoveEntity(itemId);
            packetSender.Send(new EntityDestroyed(itemId));
        }
    }

    /// <summary>
    ///     The server keeps the list of what the player wears and hands it back on joining. Only an item the player puts
    ///     on or takes off in a running game changes it, never the player being put together while joining or taken
    ///     apart while leaving.
    /// </summary>
    internal static bool IsPlayersOwnChange(bool sessionJoined, bool initialSyncCompleted) => sessionJoined && initialSyncCompleted;

    private static bool InRunningGame => IsPlayersOwnChange(Multiplayer.Joined, Multiplayer.Main && Multiplayer.Main.InitialSyncCompleted);

    private void ReportPlayerEquipment(Player player, NitroxId playerId)
    {
        if (!InRunningGame)
        {
            return;
        }
        entities.EntityMetadataChanged(player, playerId);
    }

    /// <summary>
    ///     Puts an item the server has as worn into a slot of the local player, the way the game restores a saved one:
    ///     without the game's equip path and without telling the server, where this comes from.
    /// </summary>
    public static void PutOnLocalPlayer(Equipment equipment, Pickupable pickupable, string slot)
    {
        // Both the body's metadata (PlayerMetadataProcessor) and the join's list of worn items put the items on. A second
        // time, the item would first be taken off (a new InventoryItem empties the item's old container), and that was
        // reported to the server as the player taking it off.
        if (equipment.equipment.TryGetValue(slot, out InventoryItem worn) && worn != null && worn.item == pickupable)
        {
            return;
        }

        using (PacketSuppressor<EntityMetadataUpdate>.Suppress())
        using (PacketSuppressor<EntitySpawnedByClient>.Suppress())
        {
            InventoryItem inventoryItem = new(pickupable);
            inventoryItem.container = equipment;
            pickupable.Reparent(equipment.tr);

            equipment.equipment[slot] = inventoryItem;
            equipment.UpdateCount(pickupable.GetTechType(), true);
            Equipment.SendEquipmentEvent(pickupable, 0, equipment.owner, slot);
            equipment.NotifyEquip(slot, inventoryItem);
        }
    }
}
