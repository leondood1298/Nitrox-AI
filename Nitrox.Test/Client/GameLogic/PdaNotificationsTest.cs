using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace NitroxClient.GameLogic;

/// <summary>
///     At the end of joining, the PDA ends up with the markers the server kept for the player, whatever the game raised
///     while the PDA was rebuilt. A marker of an entry that reached the player while joining stays.
/// </summary>
[TestClass]
public sealed class PdaNotificationsTest
{
    private static readonly PdaNotification readBeforeLeaving = new(PdaNotificationGroup.LOG, "Story_AuroraWarning1");
    private static readonly PdaNotification stillUnread = new(PdaNotificationGroup.LOG, "Story_AuroraWarning2");
    private static readonly PdaNotification unlockedWhileAway = new(PdaNotificationGroup.ENCYCLOPEDIA, "Peeper");
    private static readonly PdaNotification unlockedWhileJoining = new(PdaNotificationGroup.ENCYCLOPEDIA, "Stalker");

    private static readonly HashSet<PdaNotification> snapshotEntries = [readBeforeLeaving, stillUnread, unlockedWhileAway];

    [TestMethod]
    public void AMarkerTheGameRaisedAgainWhileJoiningIsRemovedWhenThePlayerHadReadIt()
    {
        PdaNotifications.Compare([readBeforeLeaving, stillUnread], [stillUnread], snapshotEntries, out List<PdaNotification> toRemove, out List<PdaNotification> toAdd);

        toRemove.Should().Equal(readBeforeLeaving);
        toAdd.Should().BeEmpty();
    }

    [TestMethod]
    public void AMarkerForAnEntryUnlockedWhileAwayIsAdded()
    {
        PdaNotifications.Compare([stillUnread], [stillUnread, unlockedWhileAway], snapshotEntries, out List<PdaNotification> toRemove, out List<PdaNotification> toAdd);

        toRemove.Should().BeEmpty();
        toAdd.Should().Equal(unlockedWhileAway);
    }

    [TestMethod]
    public void AStillUnreadMarkerTheGameDidNotRaiseIsAdded()
    {
        // Nothing in the initial sync raises encyclopedia or blueprint markers, so they would be lost
        PdaNotifications.Compare([], [stillUnread], snapshotEntries, out List<PdaNotification> toRemove, out List<PdaNotification> toAdd);

        toRemove.Should().BeEmpty();
        toAdd.Should().Equal(stillUnread);
    }

    [TestMethod]
    public void AMarkerForAnEntryAnotherPlayerUnlockedWhileThisOneWasJoiningIsKept()
    {
        // The snapshot is older than the entry, which came by packet; the server keeps its marker for this player
        PdaNotifications.Compare([stillUnread, unlockedWhileJoining], [stillUnread], snapshotEntries, out List<PdaNotification> toRemove, out List<PdaNotification> toAdd);

        toRemove.Should().BeEmpty();
        toAdd.Should().BeEmpty();
    }

    [TestMethod]
    public void TheEntriesOfASnapshotCanHaveTheMarkersOfTheirGroups()
    {
        HashSet<PdaNotification> markers = PdaNotifications.GetMarkersOfEntries(["1"], ["Story_AuroraWarning1"], ["Peeper"]);

        markers.Should().BeEquivalentTo(
        [
            new PdaNotification(PdaNotificationGroup.BLUEPRINTS, "1"),
            new PdaNotification(PdaNotificationGroup.CRAFT_TREE, "1"),
            new PdaNotification(PdaNotificationGroup.BUILDER, "1"),
            readBeforeLeaving,
            unlockedWhileAway
        ]);
    }

    [TestMethod]
    public void TheLogsClockAfterJoiningIsTheLatestOfTheGameTimeAndTheEntries()
    {
        PdaNotifications.GetLogClock(1000f, [400f, 1003f, 900f]).Should().Be(1003f, "an entry of another game timed after this game's clock is no news to this player");
        PdaNotifications.GetLogClock(1000f, [400f]).Should().Be(1000f);
    }

    [TestMethod]
    public void MarkersAreAsLongVisibleBeforeTheyCountAsReadAsTheGameMakesThem()
    {
        PdaNotifications.GetDuration(PdaNotificationGroup.BLUEPRINTS).Should().Be(3f);
        PdaNotifications.GetDuration(PdaNotificationGroup.CRAFT_TREE).Should().Be(2f);
        PdaNotifications.GetDuration(PdaNotificationGroup.BUILDER).Should().Be(2f);
        PdaNotifications.GetDuration(PdaNotificationGroup.LOG).Should().Be(3f);
        PdaNotifications.GetDuration(PdaNotificationGroup.ENCYCLOPEDIA).Should().Be(3f);
    }
}
