using System.Net;
using System.Threading.Tasks;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Server.Subnautica.Models.AppEvents;
using Nitrox.Server.Subnautica.Models.Communication;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Test.Server.Subnautica;

[TestClass]
public sealed class MapRoomScanResultSubscriptionsTest
{
    [TestMethod]
    public async Task DisconnectRemovesOnlyThatSessionsSubscriptions()
    {
        // Session ids are reused, so a later player must not inherit a departed player's Scanner Room pushes.
        MapRoomScanResultSubscriptions subscriptions = new();
        NitroxId firstRoom = new();
        NitroxId secondRoom = new();
        SessionId leaving = (ushort)1;
        SessionId staying = (ushort)2;
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
