using System;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;

namespace Nitrox.Model.Subnautica.Packets;

[Serializable]
public class PickupItem : Packet
{
    public InventoryItemEntity Item { get; }

    /// <summary>
    ///     Set when the game made the item from a source (see <see cref="ItemSource" />); the server then decides whether
    ///     this player got it.
    /// </summary>
    public ItemSource? Source { get; }

    public PickupItem(InventoryItemEntity item, ItemSource? source = null)
    {
        Item = item;
        Source = source;
    }
}
