namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     A mute lives with the player (saved, so rejoining keeps it) and is copied into the connection's context, which is
///     what other games are told about a player.
/// </summary>
internal static class PlayerMutes
{
    public static void Set(Player player, bool muted)
    {
        player.IsMuted = muted;
        if (player.PlayerContext != null)
        {
            player.PlayerContext.IsMuted = muted;
        }
    }
}
