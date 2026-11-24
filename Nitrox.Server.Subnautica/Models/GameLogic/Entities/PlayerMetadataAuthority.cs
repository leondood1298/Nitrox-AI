namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <summary>
///     What a player's body may be told. Its metadata is the player's list of equipped items, which the server also keeps
///     as the player's own (Player.EquippedItems) and hands back when the player joins.
/// </summary>
internal static class PlayerMetadataAuthority
{
    /// <param name="entityIsPlayerBody">The entity is a player's body (a PlayerEntity).</param>
    /// <param name="metadataIsEquipment">The update is a list of equipped items (PlayerMetadata).</param>
    /// <param name="senderOwnsBody">The body is the sender's own.</param>
    /// <param name="senderHasJoined">The sender finished applying the state it was given on joining.</param>
    public static PlayerMetadataVerdict Judge(bool entityIsPlayerBody, bool metadataIsEquipment, bool senderOwnsBody, bool senderHasJoined)
    {
        if (!entityIsPlayerBody)
        {
            return metadataIsEquipment ? PlayerMetadataVerdict.NOT_ON_A_PLAYER : PlayerMetadataVerdict.NOT_PLAYER_STATE;
        }
        // The game finds a body's metadata by its first component that has some: the lungs' oxygen of a damage or heal
        // report would take the place of the equipment list, and the next join would then not show what is worn.
        if (!metadataIsEquipment)
        {
            return PlayerMetadataVerdict.NOT_EQUIPMENT;
        }
        if (!senderOwnsBody)
        {
            return PlayerMetadataVerdict.OTHER_PLAYERS_BODY;
        }
        // While joining, the game builds the player up from what it was sent; what it reports then is a player still
        // being put together, not something the player did.
        if (!senderHasJoined)
        {
            return PlayerMetadataVerdict.NOT_JOINED_YET;
        }
        return PlayerMetadataVerdict.ACCEPT;
    }
}

internal enum PlayerMetadataVerdict
{
    /// <summary>
    ///     Neither a body nor an equipment list: none of this authority's business.
    /// </summary>
    NOT_PLAYER_STATE,
    ACCEPT,
    NOT_ON_A_PLAYER,
    NOT_EQUIPMENT,
    OTHER_PLAYERS_BODY,
    NOT_JOINED_YET
}
