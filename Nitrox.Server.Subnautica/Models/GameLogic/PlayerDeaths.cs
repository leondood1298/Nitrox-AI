using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     What a player's death changes on the server.
/// </summary>
/// <remarks>
///     Hardcore is the player's own game mode, not only the server's: the <c>gamemode</c> command sets it per player,
///     <c>changeservergamemode</c> sets it for every player, and the player's game shows the hardcore game-over screen
///     by that same mode. A player who dies in hardcore is out of this world for good and what they carried is gone.
/// </remarks>
internal sealed class PlayerDeaths(WorldEntityManager worldEntityManager, PlayerHandAndSeatStates handAndSeatStates, IOptions<SubnauticaServerOptions> options, ILogger<PlayerDeaths> logger)
{
    /// <summary>
    ///     How far from a death marker the player's game may report reaching it.
    /// </summary>
    private const float MARKER_MATCH_DISTANCE = 5f;

    private readonly WorldEntityManager worldEntityManager = worldEntityManager;
    private readonly PlayerHandAndSeatStates handAndSeatStates = handAndSeatStates;
    private readonly IOptions<SubnauticaServerOptions> options = options;
    private readonly ILogger<PlayerDeaths> logger = logger;

    public static bool IsPermanent(Player player) => player.GameMode == SubnauticaGameMode.HARDCORE;

    /// <returns>True if the death is permanent: the player is to be removed from the game.</returns>
    public bool Died(Player player, NitroxVector3 deathPosition)
    {
        handAndSeatStates.PlayerDied(player.SessionId);
        player.LastStoredPosition = deathPosition;
        player.LastStoredSubRootID = player.SubRootId;

        if (!IsPermanent(player))
        {
            if (options.Value.MarkDeathPointsWithBeacon)
            {
                player.DeathMarkers.Add(deathPosition);
            }
            return false;
        }

        player.IsPermaDeath = true;
        RemoveFromWorld(player);
        logger.ZLogInformation($"{player.Name} died in hardcore mode and can no longer join this world");
        return true;
    }

    /// <summary>
    ///     The player came back to where they died; the beacon there is gone.
    /// </summary>
    public static void ForgetMarker(Player player, NitroxVector3 reachedPosition)
    {
        NitroxVector3? closest = null;
        float closestDistance = MARKER_MATCH_DISTANCE;
        foreach (NitroxVector3 marker in player.DeathMarkers)
        {
            float distance = NitroxVector3.Distance(marker, reachedPosition);
            if (distance <= closestDistance)
            {
                closest = marker;
                closestDistance = distance;
            }
        }
        if (closest.HasValue)
        {
            player.DeathMarkers.Remove(closest.Value);
        }
    }

    /// <summary>
    ///     The player's body and everything in it (inventory, equipment, quick slots) leave the world, so that nothing
    ///     the dead player carried stays in the save where nobody could ever reach it.
    /// </summary>
    private void RemoveFromWorld(Player player)
    {
        if (!worldEntityManager.TryDestroyEntity(player.GameObjectId, out _))
        {
            logger.ZLogWarning($"The body of {player.Name} was not in the world");
        }
        player.EquippedItems.Clear();
        player.QuickSlotsBindingIds = [];
        player.DeathMarkers.Clear();
    }
}
