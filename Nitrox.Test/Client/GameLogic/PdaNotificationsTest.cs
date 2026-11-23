using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace NitroxClient.GameLogic;

/// <summary>
///     At the end of joining, the PDA ends up with exactly the markers the server kept for the player, whatever the
///     game raised while the PDA was rebuilt.
/// </summary>
[TestClass]
public sealed class PdaNotificationsTest
{
    private static readonly PdaNotification readBeforeLeaving = new(PdaNotificationGroup.LOG, "Story_AuroraWarning1");
    private static readonly PdaNotification stillUnread = new(PdaNotificationGroup.LOG, "Story_AuroraWarning2");
    private static readonly PdaNotification unlockedWhileAway = new(PdaNotificationGroup.ENCYCLOPEDIA, "Peeper");

    [TestMethod]
    public void AMarkerTheGameRaisedAgainWhileJoiningIsRemovedWhenThePlayerHadReadIt()
    {
        PdaNotifications.Compare([readBeforeLeaving, stillUnread], [stillUnread], out List<PdaNotification> toRemove, out List<PdaNotification> toAdd);

        toRemove.Should().Equal(readBeforeLeaving);
        toAdd.Should().BeEmpty();
    }

    [TestMethod]
    public void AMarkerForAnEntryUnlockedWhileAwayIsAdded()
    {
        PdaNotifications.Compare([stillUnread], [stillUnread, unlockedWhileAway], out List<PdaNotification> toRemove, out List<PdaNotification> toAdd);

        toRemove.Should().BeEmpty();
        toAdd.Should().Equal(unlockedWhileAway);
    }

    [TestMethod]
    public void AStillUnreadMarkerTheGameDidNotRaiseIsAdded()
    {
        // Nothing in the initial sync raises encyclopedia or blueprint markers, so they would be lost
        PdaNotifications.Compare([], [stillUnread], out List<PdaNotification> toRemove, out List<PdaNotification> toAdd);

        toRemove.Should().BeEmpty();
        toAdd.Should().Equal(stillUnread);
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
