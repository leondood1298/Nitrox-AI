using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     The smell the sender left on an item they dropped (Pickupable.Drop adds a Smell). A Stalker that picks the item up
///     befriends whoever it smells of (Stalker.OnShinyPickUp), and that Stalker may be simulated by another player.
/// </summary>
[Serializable]
public class DroppedItemSmell : Packet
{
    public NitroxId ItemId { get; }

    /// <summary>
    ///     The id of the sender's player object.
    /// </summary>
    public NitroxId PlayerId { get; }

    public float Strength { get; }

    /// <summary>
    ///     How much strength the smell loses every second.
    /// </summary>
    public float Falloff { get; }

    public DroppedItemSmell(NitroxId itemId, NitroxId playerId, float strength, float falloff)
    {
        ItemId = itemId;
        PlayerId = playerId;
        Strength = strength;
        Falloff = falloff;
    }
}
