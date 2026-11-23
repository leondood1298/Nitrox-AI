using System.Net;
using System.Threading.Tasks;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Server.Subnautica.Models.AppEvents;
using Nitrox.Server.Subnautica.Models.Communication;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Test.Server.GameLogic.Entities;

[TestClass]
public sealed class MapRoomScanResultSubscriptionsTest
{
    [TestMethod]
    public void TracksSubscriptionsPerRoomAndSession()
    {
        MapRoomScanResultSubscriptions subscriptions = new();
        NitroxId firstRoom = new();
        NitroxId secondRoom = new();
        SessionId firstPlayer = 1;
        SessionId secondPlayer = 2;

        subscriptions.Set(firstRoom, firstPlayer, true);
        subscriptions.Set(firstRoom, secondPlayer, true);
        subscriptions.Set(secondRoom, secondPlayer, true);

        Assert.IsTrue(subscriptions.Contains(firstRoom, firstPlayer));
        Assert.IsTrue(subscriptions.Contains(firstRoom, secondPlayer));
        Assert.IsFalse(subscriptions.Contains(secondRoom, firstPlayer));
        Assert.IsTrue(subscriptions.Contains(secondRoom, secondPlayer));
    }

    [TestMethod]
    public void UnsubscribeIsIdempotentAndDoesNotAffectOtherSessions()
    {
        MapRoomScanResultSubscriptions subscriptions = new();
        NitroxId room = new();
        SessionId firstPlayer = 1;
        SessionId secondPlayer = 2;
        subscriptions.Set(room, firstPlayer, true);
        subscriptions.Set(room, secondPlayer, true);

        subscriptions.Set(room, firstPlayer, false);
        subscriptions.Set(room, firstPlayer, false);

        Assert.IsFalse(subscriptions.Contains(room, firstPlayer));
        Assert.IsTrue(subscriptions.Contains(room, secondPlayer));
    }

    [TestMethod]
    public void RemovingRoomReturnsItsSubscribersAndLeavesOtherRoomsUntouched()
    {
        MapRoomScanResultSubscriptions subscriptions = new();
        NitroxId removedRoom = new();
        NitroxId retainedRoom = new();
        subscriptions.Set(removedRoom, 1, true);
        subscriptions.Set(removedRoom, 2, true);
        subscriptions.Set(retainedRoom, 2, true);

        List<SessionId> removed = subscriptions.RemoveRoom(removedRoom);

        CollectionAssert.AreEquivalent(new SessionId[] { 1, 2 }, removed);
        Assert.IsFalse(subscriptions.Contains(removedRoom, 1));
        Assert.IsFalse(subscriptions.Contains(removedRoom, 2));
        Assert.IsTrue(subscriptions.Contains(retainedRoom, 2));
    }

    [TestMethod]
    public async Task DisconnectRemovesOnlyThatSessionsSubscriptions()
    {
        // Session ids are reused, so a later player must not inherit a departed player's Scanner Room pushes.
        MapRoomScanResultSubscriptions subscriptions = new();
        NitroxId firstRoom = new();
        NitroxId secondRoom = new();
        SessionId leaving = 1;
        SessionId staying = 2;
        subscriptions.Set(firstRoom, leaving, true);
        subscriptions.Set(secondRoom, leaving, true);
        subscriptions.Set(firstRoom, staying, true);

        await subscriptions.OnEventAsync(new ISessionCleaner.Args(
            new SessionManager.Session(leaving, new IPEndPoint(IPAddress.Loopback, 11000)), 1));

        Assert.IsFalse(subscriptions.Contains(firstRoom, leaving));
        Assert.IsFalse(subscriptions.Contains(secondRoom, leaving));
        Assert.IsTrue(subscriptions.Contains(firstRoom, staying));
    }
}
