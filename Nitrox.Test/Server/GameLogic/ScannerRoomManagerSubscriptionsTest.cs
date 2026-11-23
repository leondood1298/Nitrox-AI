using System.Net;
using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.AppEvents;
using Nitrox.Server.Subnautica.Models.Communication;
using Nitrox.Test.Server.Helper;
using Nitrox.Test.Server.Subnautica;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     A room's scan results only go to the players who have its scanner screen open.
/// </summary>
[TestClass]
public sealed class ScannerRoomManagerSubscriptionsTest
{
    private static readonly NitroxTechType quartz = new("Quartz");

    private ConnectedPlayers players;
    private ScannerRoomScenarioFixture scenario;
    private MapRoomEntity firstRoom;
    private MapRoomEntity secondRoom;
    private Player firstPlayer;
    private Player secondPlayer;

    [TestInitialize]
    public void Init()
    {
        players = new ConnectedPlayers();
        scenario = new ScannerRoomScenarioFixture(playerManager: players.PlayerManager);
        firstRoom = scenario.Room;
        secondRoom = scenario.AddRoom(new MapRoomEntity(new NitroxId("00000000-0000-0000-0000-000000000110"), ScannerRoomScenarioFixture.RoomParentId, new NitroxInt3(5, -2, 9)));
        firstPlayer = players.Connect("First");
        secondPlayer = players.Connect("Second");
    }

    [TestMethod]
    public async Task SubscriberIsSentTheCurrentResultsAtOnce()
    {
        NitroxId resourceId = AddResult(firstRoom);

        await scenario.Manager.SetSubscriptionAsync(firstPlayer, firstRoom.Id, true);

        MapRoomScanResultSnapshot current = scenario.SentTo<MapRoomScanResultSnapshot>(firstPlayer).Single();
        Assert.IsTrue(current.IsServerResponse && current.Granted);
        Assert.AreEqual(firstRoom.Id, current.MapRoomId);
        Assert.AreEqual(resourceId.ToString(), current.Results.Single().ResourceId);
        Assert.AreEqual(0, scenario.SentTo<MapRoomScanResultSnapshot>(secondPlayer).Count);
    }

    [TestMethod]
    public async Task ChangesGoToTheSubscribersOfThatRoomOnly()
    {
        NitroxId inFirstRoom = AddResult(firstRoom);
        NitroxId inSecondRoom = AddResult(secondRoom);
        await scenario.Manager.SetSubscriptionAsync(firstPlayer, firstRoom.Id, true);
        await scenario.Manager.SetSubscriptionAsync(secondPlayer, firstRoom.Id, true);
        await scenario.Manager.SetSubscriptionAsync(secondPlayer, secondRoom.Id, true);

        await scenario.Manager.EntityRemovedAsync(firstPlayer, inFirstRoom);
        await scenario.Manager.EntityRemovedAsync(firstPlayer, inSecondRoom);

        Assert.AreEqual(firstRoom.Id, scenario.SentTo<MapRoomScanResultChanged>(firstPlayer).Single().MapRoomId);
        CollectionAssert.AreEqual(new[] { firstRoom.Id, secondRoom.Id }, scenario.SentTo<MapRoomScanResultChanged>(secondPlayer).Select(change => change.MapRoomId).ToArray());
    }

    [TestMethod]
    public async Task UnsubscribeIsIdempotentAndDoesNotAffectOtherSessions()
    {
        NitroxId resourceId = AddResult(firstRoom);
        await scenario.Manager.SetSubscriptionAsync(firstPlayer, firstRoom.Id, true);
        await scenario.Manager.SetSubscriptionAsync(secondPlayer, firstRoom.Id, true);

        await scenario.Manager.SetSubscriptionAsync(firstPlayer, firstRoom.Id, false);
        await scenario.Manager.SetSubscriptionAsync(firstPlayer, firstRoom.Id, false);
        await scenario.Manager.EntityRemovedAsync(firstPlayer, resourceId);

        Assert.AreEqual(0, scenario.SentTo<MapRoomScanResultChanged>(firstPlayer).Count);
        Assert.AreEqual(1, scenario.SentTo<MapRoomScanResultChanged>(secondPlayer).Count);
    }

    [TestMethod]
    public async Task RemovingARoomTellsItsSubscribersAndLeavesOtherRoomsUntouched()
    {
        AddResult(firstRoom);
        NitroxId inSecondRoom = AddResult(secondRoom);
        await scenario.Manager.SetSubscriptionAsync(firstPlayer, firstRoom.Id, true);
        await scenario.Manager.SetSubscriptionAsync(secondPlayer, firstRoom.Id, true);
        await scenario.Manager.SetSubscriptionAsync(secondPlayer, secondRoom.Id, true);
        int snapshotsBefore = scenario.Sent<MapRoomScanResultSnapshot>().Count;
        Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(firstRoom.Id).HasValue);

        await scenario.Manager.RoomDeconstructedAsync(firstPlayer, firstRoom);
        await scenario.Manager.EntityRemovedAsync(firstPlayer, inSecondRoom);

        MapRoomScanResultSnapshot[] cleared = scenario.Sent<MapRoomScanResultSnapshot>().Skip(snapshotsBefore).ToArray();
        Assert.AreEqual(2, cleared.Length, "Both players who had the removed room's screen open are told its results are gone.");
        Assert.IsTrue(cleared.All(snapshot => snapshot.MapRoomId == firstRoom.Id && snapshot.Results.Count == 0));
        Assert.AreEqual(secondRoom.Id, scenario.SentTo<MapRoomScanResultChanged>(secondPlayer).Single().MapRoomId, "The other room's subscription is still there.");
    }

    [TestMethod]
    public async Task DisconnectRemovesOnlyThatSessionsSubscriptions()
    {
        // Session ids are reused, so a later player must not inherit a departed player's Scanner Room pushes.
        NitroxId inFirstRoom = AddResult(firstRoom);
        NitroxId inSecondRoom = AddResult(secondRoom);
        await scenario.Manager.SetSubscriptionAsync(firstPlayer, firstRoom.Id, true);
        await scenario.Manager.SetSubscriptionAsync(firstPlayer, secondRoom.Id, true);
        await scenario.Manager.SetSubscriptionAsync(secondPlayer, firstRoom.Id, true);

        await scenario.Manager.OnEventAsync(new ISessionCleaner.Args(
            new SessionManager.Session(firstPlayer.SessionId, new IPEndPoint(IPAddress.Loopback, 11000)), 1));
        await scenario.Manager.EntityRemovedAsync(secondPlayer, inFirstRoom);
        await scenario.Manager.EntityRemovedAsync(secondPlayer, inSecondRoom);

        Assert.AreEqual(0, scenario.SentTo<MapRoomScanResultChanged>(firstPlayer).Count);
        Assert.AreEqual(firstRoom.Id, scenario.SentTo<MapRoomScanResultChanged>(secondPlayer).Single().MapRoomId);
    }

    /// <summary>
    ///     Puts one found resource into the room's results, as an accepted scan report does.
    /// </summary>
    private static NitroxId AddResult(MapRoomEntity room)
    {
        NitroxId resourceId = new();
        Assert.IsTrue(room.TryApplyScanResult(room.ScanResultGeneration, new MapRoomScanResultRecord(resourceId.ToString(), quartz, new NitroxVector3(1f, 2f, 3f))));
        return resourceId;
    }
}
