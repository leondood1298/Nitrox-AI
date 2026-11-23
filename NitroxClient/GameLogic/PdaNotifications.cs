using System;
using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;

namespace NitroxClient.GameLogic;

/// <summary>
///     The unread markers of the PDA (blueprints, log and encyclopedia entries), which the server keeps for each player.
///     The game keeps them in its own save. The initial sync rebuilds the PDA through the game's own calls instead, which
///     left markers on what happened to raise one while joining and none on what was still unread.
/// </summary>
public class PdaNotifications
{
    private readonly IPacketSender packetSender;

    public PdaNotifications(IPacketSender packetSender)
    {
        this.packetSender = packetSender;
    }

    /// <summary>
    ///     Called when the game adds a marker and when one is read or removed.
    /// </summary>
    public void Changed(NotificationManager.Group group, string key, bool unread)
    {
        // While joining, the PDA is rebuilt and the stored markers are restored afterwards; what the game raises meanwhile is no news
        if (!Multiplayer.Main || !Multiplayer.Main.InitialSyncCompleted || !PdaNotification.IsKept((int)group))
        {
            return;
        }
        packetSender.Send(new PdaNotificationChanged(new PdaNotification((PdaNotificationGroup)group, key), unread));
    }

    /// <summary>
    ///     Leaves the PDA with the markers the server kept for this player, once the PDA's content is restored.
    /// </summary>
    /// <param name="stored">The markers the server kept when this player began joining.</param>
    /// <param name="ofSnapshotEntries">The markers the entries of the snapshot can have.</param>
    public void Restore(List<PdaNotification> stored, ISet<PdaNotification> ofSnapshotEntries)
    {
        NotificationManager manager = NotificationManager.main;
        if (!manager)
        {
            Log.Error($"[{nameof(PdaNotifications)}] No NotificationManager to restore {stored.Count} unread PDA markers into");
            return;
        }

        // PDALog.Deserialize took the log's clock before the initial sync set the game time. Its next update would mark
        // every log entry written in between as new. Entries of other games can be timed a little after this game's
        // clock; the stored markers say whether they are new.
        if (DayNightCycle.main)
        {
            PDALog.time = GetLogClock(DayNightCycle.main.timePassedAsFloat, PDALog.entries.Values.Select(entry => entry.timestamp));
        }

        List<PdaNotification> current = manager.notifications.Keys
                                               .Where(id => PdaNotification.IsKept((int)id.group))
                                               .Select(id => new PdaNotification((PdaNotificationGroup)id.group, id.key))
                                               .ToList();
        Compare(current, stored, ofSnapshotEntries, out List<PdaNotification> toRemove, out List<PdaNotification> toAdd);

        using (PacketSuppressor<PdaNotificationChanged>.Suppress())
        {
            foreach (PdaNotification notification in toRemove)
            {
                manager.Remove((NotificationManager.Group)notification.Group, notification.Key);
            }

            NotificationManager.SerializedData data = new();
            foreach (PdaNotification notification in toAdd)
            {
                float duration = GetDuration(notification.Group);
                data.notifications[new NotificationManager.NotificationId((NotificationManager.Group)notification.Group, notification.Key)] =
                    new NotificationManager.NotificationData { duration = duration, timeLeft = duration };
            }
            // The game's own restore: it leaves out markers of what it does not have, such as a builder marker of a
            // blueprint that cannot be built, which the server keeps for a player who was away.
            manager.Deserialize(data);
        }

        foreach (PdaNotification notification in toAdd)
        {
            if (!manager.Contains((NotificationManager.Group)notification.Group, notification.Key))
            {
                packetSender.Send(new PdaNotificationChanged(notification, false));
            }
        }
        Log.Info($"Restored {stored.Count} unread PDA markers ({toAdd.Count} added, {toRemove.Count} removed)");
    }

    /// <summary>
    ///     What has to change so that the markers the PDA has are the stored ones. A marker of an entry the snapshot does
    ///     not have is kept: the entry came by packet while this player was joining, after the server made the snapshot,
    ///     and the server keeps the marker for this player.
    /// </summary>
    internal static void Compare(IEnumerable<PdaNotification> current, IEnumerable<PdaNotification> stored, ISet<PdaNotification> ofSnapshotEntries, out List<PdaNotification> toRemove,
                                 out List<PdaNotification> toAdd)
    {
        HashSet<PdaNotification> currentSet = [.. current];
        HashSet<PdaNotification> storedSet = [.. stored];
        toRemove = currentSet.Where(notification => !storedSet.Contains(notification) && ofSnapshotEntries.Contains(notification)).ToList();
        toAdd = storedSet.Where(notification => !currentSet.Contains(notification)).ToList();
    }

    /// <summary>
    ///     The markers the entries of a snapshot can have: the blueprints' (KnownTech.UnreadAdd) and the log's and the
    ///     encyclopedia's, time capsules included.
    /// </summary>
    /// <param name="techTypeKeys">The known blueprints, keyed as the game keys their markers (TechTypeExtensions.EncodeKey).</param>
    internal static HashSet<PdaNotification> GetMarkersOfEntries(IEnumerable<string> techTypeKeys, IEnumerable<string> logKeys, IEnumerable<string> encyclopediaKeys)
    {
        HashSet<PdaNotification> markers = [];
        markers.UnionWith(techTypeKeys.SelectMany(PdaNotification.ForBlueprint));
        markers.UnionWith(logKeys.Select(key => new PdaNotification(PdaNotificationGroup.LOG, key)));
        markers.UnionWith(encyclopediaKeys.Select(key => new PdaNotification(PdaNotificationGroup.ENCYCLOPEDIA, key)));
        return markers;
    }

    public static HashSet<PdaNotification> GetMarkersOfEntries(InitialPlayerSync sync)
    {
        InitialPDAData pdaData = sync.PDAData;
        return GetMarkersOfEntries(pdaData.KnownTechTypes.Select(techType => techType.ToUnity().EncodeKey()),
                                   pdaData.PDALogEntries.Select(entry => entry.Key),
                                   pdaData.EncyclopediaEntries.Concat(sync.CollectedTimeCapsules.Select(capsule => capsule.Id)));
    }

    /// <summary>
    ///     The log's clock after joining: the game time, or the time of the latest entry if that is later. PDALog.OnUpdate
    ///     marks the entries timed between its last clock and the game time, which the stored markers decided already for
    ///     the entries this player joined with.
    /// </summary>
    internal static float GetLogClock(float now, IEnumerable<float> entryTimes)
    {
        return entryTimes.Aggregate(now, Math.Max);
    }

    /// <summary>
    ///     Whether PDALog.OnUpdate may move the log's clock to the game time. A correction of the game clock can move it
    ///     back, after which the log would mark every entry timed since the corrected time again as new.
    /// </summary>
    public static bool MayAdvanceLogClock()
    {
        return !DayNightCycle.main || DayNightCycle.main.timePassedAsFloat >= PDALog.time;
    }

    /// <summary>
    ///     How long the game shows a marker before it counts as read, as it gives it when it adds one (KnownTech.UnreadAdd,
    ///     PDALog.Add, PDAEncyclopedia.Add).
    /// </summary>
    internal static float GetDuration(PdaNotificationGroup group)
    {
        return group switch
        {
            PdaNotificationGroup.BUILDER or PdaNotificationGroup.CRAFT_TREE => 2f,
            _ => 3f
        };
    }
}
