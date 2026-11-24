using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     From the server to one player: another player got an item first that this player's game had already given to
///     this player. The game is corrected by a packet sent with this one, or, for an item it made itself, by removing it.
/// </summary>
[Serializable]
public class ItemClaimLost : Packet
{
    public NitroxId ItemId { get; }
    public NitroxTechType TechType { get; }

    /// <summary>
    ///     The name of the player who got the item, or who it is kept for.
    /// </summary>
    public string WinnerName { get; }

    /// <summary>
    ///     The item only exists in this player's game (it was made there from a source) and has to be removed.
    /// </summary>
    public bool RemoveLocalCopy { get; }

    /// <summary>
    ///     The item is not taken yet but kept for <see cref="WinnerName" /> for a while: a fabricator's result is its
    ///     crafter's for a few seconds.
    /// </summary>
    public bool Reserved { get; }

    public ItemClaimLost(NitroxId itemId, NitroxTechType techType, string winnerName, bool removeLocalCopy, bool reserved)
    {
        ItemId = itemId;
        TechType = techType;
        WinnerName = winnerName;
        RemoveLocalCopy = removeLocalCopy;
        Reserved = reserved;
    }
}
