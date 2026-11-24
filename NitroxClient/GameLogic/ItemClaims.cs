using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     This game gives an item to its player at once and tells the server afterwards. When another player got the item
///     first, the server says so: the item is then put where the server has it (by a packet sent along) or, if this game
///     made it, removed, and the player is told who got it.
/// </summary>
public class ItemClaims
{
    private readonly Entities entities;
    private readonly SimulationOwnership simulationOwnership;

    public ItemClaims(Entities entities, SimulationOwnership simulationOwnership)
    {
        this.entities = entities;
        this.simulationOwnership = simulationOwnership;
    }

    public void Lost(ItemClaimLost claim)
    {
        if (claim.RemoveLocalCopy)
        {
            RemoveMadeItem(claim.ItemId);
        }

        string itemName = Language.main.Get(claim.TechType.ToUnity().AsString());
        string template = Language.main.Get(claim.Reserved ? "Nitrox_ItemReservedForCrafter" : "Nitrox_ItemTakenFirst");
        ErrorMessage.AddMessage(FormatMessage(template, claim.WinnerName, itemName));
    }

    internal static string FormatMessage(string template, string playerName, string itemName)
    {
        return template.Replace("{PLAYER}", playerName).Replace("{ITEM}", itemName);
    }

    private void RemoveMadeItem(NitroxId itemId)
    {
        entities.RemoveEntity(itemId);
        simulationOwnership.DropSimulationFrom(itemId);
        EntityPositionBroadcaster.Instance.StopWatchingEntity(itemId);
        if (NitroxEntity.TryGetObjectFrom(itemId, out GameObject item))
        {
            Entities.DestroyObject(item);
        }
    }
}
