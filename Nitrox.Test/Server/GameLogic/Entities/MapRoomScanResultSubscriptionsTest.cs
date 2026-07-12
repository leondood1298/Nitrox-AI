using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
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
}
