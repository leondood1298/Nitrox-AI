using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Keeps each player's unread PDA markers. A player's own game reports the markers it raises and the ones the player
///     reads. For a player who is away, the server adds the markers that player's game would have raised for an entry
///     another player unlocked.
/// </summary>
internal sealed class PdaNotificationManager(PlayerManager playerManager)
{
    private readonly PlayerManager playerManager = playerManager;

    public void Change(Player player, PdaNotification notification, bool unread)
    {
        if (unread)
        {
            player.UnreadPdaNotifications.Add(notification);
        }
        else
        {
            player.UnreadPdaNotifications.Remove(notification);
        }
    }

    /// <param name="verbose">The game only marks a blueprint it learns verbosely (KnownTech.NotifyAdd).</param>
    public void BlueprintLearned(NitroxTechType techType, bool verbose)
    {
        if (verbose && TryGetTechTypeKey(techType, out string key))
        {
            MarkUnreadForAbsentPlayers(PdaNotification.ForBlueprint(key));
        }
    }

    /// <remarks>The game marks every log entry it adds (PDALog.Add, or PDALog.OnUpdate once a delayed entry is due).</remarks>
    public void LogEntryAdded(string key)
    {
        MarkUnreadForAbsentPlayers(new PdaNotification(PdaNotificationGroup.LOG, key));
    }

    /// <param name="verbose">The game only marks an entry it adds verbosely (PDAEncyclopedia.Add).</param>
    public void EncyclopediaEntryAdded(string key, bool verbose)
    {
        if (verbose)
        {
            MarkUnreadForAbsentPlayers(new PdaNotification(PdaNotificationGroup.ENCYCLOPEDIA, key));
        }
    }

    /// <summary>
    ///     The game keys a blueprint's markers by the number of its tech type (TechTypeExtensions.EncodeKey).
    /// </summary>
    internal static bool TryGetTechTypeKey(NitroxTechType? techType, [NotNullWhen(true)] out string? key)
    {
        if (techType != null && Enum.TryParse(techType.Name, out TechType value))
        {
            key = ((int)value).ToString(CultureInfo.InvariantCulture);
            return true;
        }
        key = null;
        return false;
    }

    private void MarkUnreadForAbsentPlayers(params IEnumerable<PdaNotification> notifications)
    {
        foreach (Player player in playerManager.GetAllPlayers())
        {
            // A player who plays gets the marker from its own game, which reports it. One who is still joining was sent
            // the markers it had when it began joining, so the server keeps this one for it.
            if (player.IsOnline && player.HasFinishedInitialSync)
            {
                continue;
            }
            foreach (PdaNotification notification in notifications)
            {
                player.UnreadPdaNotifications.Add(notification);
            }
        }
    }
}
