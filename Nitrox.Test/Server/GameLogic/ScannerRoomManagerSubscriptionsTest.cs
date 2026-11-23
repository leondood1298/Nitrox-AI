using System.Net;
using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.AppEvents;
using Nitrox.Server.Subnautica.Models.Communication;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Test.Server.Helper;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.GameLogic;

/// <summary>
///     What a room's scan finds only goes to the players who subscribed to it, and is held in memory only.
/// </summary>
[TestClass]
public sealed class ScannerRoomManagerSubscriptionsTest
{
    private const long GENERATION = 5;
    private static readonly NitroxTechType quartz = new("Quartz");

    private ConnectedPlayers players;
    private ScannerRoomScenarioFixture scenario;
    private MapRoomEntity firstRoom;
    private MapRoomEntity secondRoom;
    private ServerPlayer simulator;
    private ServerPlayer firstPlayer;
    private ServerPlayer secondPlayer;

    [TestInitialize]
    public void Init()
    {
        players = new ConnectedPlayers();
        scenario = new ScannerRoomScenarioFixture(playerManager: players.PlayerManager);
        firstRoom = scenario.Room;
        secondRoom = scenario.AddRoom(new MapRoomEntity(new NitroxId("00000000-0000-0000-0000-000000000110"), ScannerRoomScenarioFixture.RoomParentId, new NitroxInt3(5, -2, 9)));
        simulator = players.Connect("Simulator");
        firstPlayer = players.Connect("First");
        secondPlayer = players.Connect("Second");
    }

    [TestMethod]
    public async Task SubscriberIsSentTheCurrentResultsAndTypesAtOnce()
    {
        NitroxId resourceId = await AddResultAsync(firstRoom);
        await scenario.Manager.ReportScanTypesAsync(simulator, new MapRoomScanTypesReport(firstRoom.Id, [quartz], [quartz], NitroxVector3.Zero, 300f));

        await scenario.Manager.SetSubscriptionAsync(firstPlayer, firstRoom.Id, true);

        MapRoomScanSnapshot current = scenario.SentTo<MapRoomScanSnapshot>(firstPlayer).Single();
        Assert.AreEqual(firstRoom.Id, current.MapRoomId);
        Assert.AreEqual(GENERATION, current.Generation);
        Assert.AreEqual(resourceId.ToString(), current.Results.Single().ResourceId);
        CollectionAssert.AreEqual(new[] { quartz }, current.AvailableTypes);
        Assert.AreEqual(0, scenario.SentTo<MapRoomScanSnapshot>(secondPlayer).Count);
    }

    [TestMethod]
    public async Task AfterAServerStartNothingIsHeldUntilTheSimulatingPlayerReports()
    {
        await AddResultAsync(firstRoom);
        // The world a restarted server loads is the entity registry. Scan results are not in it.
        ScannerRoomManager restarted = ScannerRoomScenarioFixture.CreateManager(scenario.EntityRegistry, scenario.Ownership, scenario.Diagnostics, scenario.PacketSender,
            players.PlayerManager);
        restarted.WorldLoaded();

        await restarted.SetSubscriptionAsync(firstPlayer, firstRoom.Id, true);
        MapRoomScanSnapshot afterStart = scenario.SentTo<MapRoomScanSnapshot>(firstPlayer).Single();
        Assert.AreEqual(GENERATION, afterStart.Generation, "The scan target and its generation are saved with the room.");
        Assert.AreEqual(0, afterStart.Results.Count);
        Assert.AreEqual(0, afterStart.AvailableTypes.Count);

        MapRoomScanResultRecord found = new("found-again", quartz, new NitroxVector3(1f, 2f, 3f));
        await restarted.ReportScanAsync(simulator, new MapRoomScanReport(firstRoom.Id, GENERATION, NitroxVector3.Zero, 300f, [found]));

        MapRoomScanSnapshot republished = scenario.SentTo<MapRoomScanSnapshot>(firstPlayer).Last();
        Assert.AreEqual("found-again", republished.Results.Single().ResourceId);
        Assert.AreEqual(1, restarted.GetSnapshot(firstRoom.Id)!.ScanResultCount);
    }

    [TestMethod]
    public async Task NewTargetEmptiesTheResultsUntilTheSimulatingPlayerReportsForIt()
    {
        await AddResultAsync(firstRoom);
        NitroxTechType titanium = new("Titanium");

        await scenario.UpdateMetadataAsync(firstPlayer, firstRoom.Id, new MapRoomMetadata(titanium, 0, GENERATION));
        Assert.AreEqual(0, scenario.Manager.GetSnapshot(firstRoom.Id)!.ScanResultCount);
        await scenario.ReportScanAsync(simulator, firstRoom, new MapRoomScanResultRecord("titanium", titanium, new NitroxVector3(1f, 2f, 3f)));
        await scenario.Manager.SetSubscriptionAsync(firstPlayer, firstRoom.Id, true);

        MapRoomScanSnapshot current = scenario.SentTo<MapRoomScanSnapshot>(firstPlayer).Single();
        Assert.AreEqual(GENERATION + 1, current.Generation);
        Assert.AreEqual("titanium", current.Results.Single().ResourceId);
    }

    [TestMethod]
    public async Task ChangesGoToTheSubscribersOfThatRoomOnly()
    {
        NitroxId inFirstRoom = await AddResultAsync(firstRoom);
        NitroxId inSecondRoom = await AddResultAsync(secondRoom);
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
        NitroxId resourceId = await AddResultAsync(firstRoom);
        await scenario.Manager.SetSubscriptionAsync(firstPlayer, firstRoom.Id, true);
        await scenario.Manager.SetSubscriptionAsync(secondPlayer, firstRoom.Id, true);

        await scenario.Manager.SetSubscriptionAsync(firstPlayer, firstRoom.Id, false);
        await scenario.Manager.SetSubscriptionAsync(firstPlayer, firstRoom.Id, false);
        await scenario.Manager.EntityRemovedAsync(firstPlayer, resourceId);

        Assert.AreEqual(0, scenario.SentTo<MapRoomScanResultChanged>(firstPlayer).Count);
        Assert.AreEqual(1, scenario.SentTo<MapRoomScanResultChanged>(secondPlayer).Count);
    }

    [TestMethod]
    public async Task RemovingARoomEndsItsSubscriptionsAndDropsItsResults()
    {
        NitroxId inFirstRoom = await AddResultAsync(firstRoom);
        NitroxId inSecondRoom = await AddResultAsync(secondRoom);
        await scenario.Manager.SetSubscriptionAsync(firstPlayer, firstRoom.Id, true);
        await scenario.Manager.SetSubscriptionAsync(secondPlayer, secondRoom.Id, true);
        Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(firstRoom.Id).HasValue);

        await scenario.Manager.RoomDeconstructedAsync(firstPlayer, firstRoom);
        // A room that is built again in the same place gets the same id.
        MapRoomEntity rebuilt = scenario.AddRoom(new MapRoomEntity(firstRoom.Id, ScannerRoomScenarioFixture.RoomParentId, firstRoom.Cell));
        await scenario.Manager.EntityRemovedAsync(firstPlayer, inFirstRoom);
        await scenario.Manager.EntityRemovedAsync(firstPlayer, inSecondRoom);

        Assert.AreEqual(0, scenario.Manager.GetSnapshot(rebuilt.Id)!.ScanResultCount);
        Assert.AreEqual(0, scenario.SentTo<MapRoomScanResultChanged>(firstPlayer).Count, "The subscription was to the room that is gone.");
        Assert.AreEqual(secondRoom.Id, scenario.SentTo<MapRoomScanResultChanged>(secondPlayer).Single().MapRoomId, "The other room's subscription is still there.");
    }

    [TestMethod]
    public async Task DisconnectRemovesOnlyThatSessionsSubscriptions()
    {
        // Session ids are reused, so a later player must not inherit a departed player's Scanner Room pushes.
        NitroxId inFirstRoom = await AddResultAsync(firstRoom);
        NitroxId inSecondRoom = await AddResultAsync(secondRoom);
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
    ///     Lets the room's simulating player report one found resource.
    /// </summary>
    private async Task<NitroxId> AddResultAsync(MapRoomEntity room)
    {
        NitroxId resourceId = new();
        scenario.StartScan(simulator, room, quartz, GENERATION);
        await scenario.ReportScanAsync(simulator, room, new MapRoomScanResultRecord(resourceId.ToString(), quartz, new NitroxVector3(1f, 2f, 3f)));
        Assert.AreEqual(1, scenario.Manager.GetSnapshot(room.Id)!.ScanResultCount);
        return resourceId;
    }
}
