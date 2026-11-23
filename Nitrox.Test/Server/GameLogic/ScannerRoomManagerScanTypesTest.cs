using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Test.Server.Helper;
using Nitrox.Test.Server.Subnautica;
using static Nitrox.Server.Subnautica.Models.GameLogic.ScannerRoomManager;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     The types a room offers to scan for.
/// </summary>
[TestClass]
public sealed class ScannerRoomManagerScanTypesTest
{
    private static readonly NitroxTechType quartz = new("Quartz");
    private static readonly NitroxTechType titanium = new("Titanium");

    private readonly MapRoomEntity room = new(new NitroxId(), new NitroxId(), new NitroxInt3());
    private readonly RoomScan scan = new();

    [TestMethod]
    public void ReportIsStoredInAFixedOrder()
    {
        Assert.IsTrue(TryApplyScanTypesReport(room, scan, Report([titanium, quartz]), [], NitroxVector3.Zero, out bool changed));

        Assert.IsTrue(changed);
        CollectionAssert.AreEqual(new[] { quartz, titanium }, scan.AvailableTypes);
        Assert.AreEqual(1, scan.AvailableTypesVersion);
    }

    [TestMethod]
    public void ReplayChangesNothingAndInvalidTypesAndWrongRoomAreRejected()
    {
        Assert.IsTrue(TryApplyScanTypesReport(room, scan, Report([quartz]), [], NitroxVector3.Zero, out _));

        Assert.IsTrue(TryApplyScanTypesReport(room, scan, Report([quartz]), [], NitroxVector3.Zero, out bool changed));
        Assert.IsFalse(changed);
        Assert.IsFalse(TryApplyScanTypesReport(room, scan, Report([quartz, quartz]), [], NitroxVector3.Zero, out _));
        Assert.IsFalse(TryApplyScanTypesReport(room, scan, Report([NitroxTechType.None]), [], NitroxVector3.Zero, out _));
        Assert.IsFalse(TryApplyScanTypesReport(room, scan, Report([quartz], [titanium, titanium]), [], NitroxVector3.Zero, out _));
        Assert.IsFalse(TryApplyScanTypesReport(room, scan, Report([quartz], [NitroxTechType.None]), [], NitroxVector3.Zero, out _));
        Assert.IsFalse(TryApplyScanTypesReport(room, scan,
            new MapRoomScanTypesReport(new NitroxId(), [titanium], [titanium], NitroxVector3.Zero, 300f), [], NitroxVector3.Zero, out _));
        Assert.AreEqual(1, scan.AvailableTypesVersion);
        CollectionAssert.AreEqual(new[] { quartz }, scan.AvailableTypes);
    }

    [TestMethod]
    public void FirstEmptyReportCountsAsAChangeAndItsReplayDoesNot()
    {
        Assert.IsTrue(TryApplyScanTypesReport(room, scan, Report([], []), [], NitroxVector3.Zero, out bool changed));
        Assert.IsTrue(TryApplyScanTypesReport(room, scan, Report([], []), [], NitroxVector3.Zero, out bool changedAgain));

        Assert.IsTrue(changed, "Until the first report, the clients do not know that the server's list is one to show.");
        Assert.IsFalse(changedAgain);
        Assert.AreEqual(0, scan.AvailableTypes.Count);
        Assert.AreEqual(1, scan.AvailableTypesVersion);
    }

    [TestMethod]
    public void ReportIsUnitedOnlyWithDetectableExactWorldTypesInRange()
    {
        NitroxTechType lead = new("Lead");
        List<WorldEntity> worldEntities =
        [
            World(quartz, new NitroxVector3(100f, 0f, 0f)),
            World(titanium, new NitroxVector3(100f, 0f, 0f)),
            World(new NitroxTechType("ShaleChunk"), new NitroxVector3(301f, 0f, 0f))
        ];

        Assert.IsTrue(TryApplyScanTypesReport(room, scan, Report([lead], [quartz]), worldEntities, NitroxVector3.Zero, out _));

        CollectionAssert.AreEqual(new[] { lead, quartz }, scan.AvailableTypes);
    }

    [TestMethod]
    public void ReportRejectsInvalidOriginAndRange()
    {
        Assert.IsFalse(TryApplyScanTypesReport(room, scan,
            new MapRoomScanTypesReport(room.Id, [], [], new NitroxVector3(float.PositiveInfinity, 0f, 0f), 300f), [], NitroxVector3.Zero, out _));
        Assert.IsFalse(TryApplyScanTypesReport(room, scan, new MapRoomScanTypesReport(room.Id, [], [], NitroxVector3.Zero, 501f), [], NitroxVector3.Zero, out _));
        Assert.IsFalse(TryApplyScanTypesReport(room, scan,
            new MapRoomScanTypesReport(room.Id, [], [], new NitroxVector3(513f, 0f, 0f), 300f), [], NitroxVector3.Zero, out _));
        Assert.AreEqual(0, scan.AvailableTypesVersion);
    }

    [TestMethod]
    public async Task ReportIsAnsweredToItsSenderAndPassedOnToSubscribersOnlyWhenItChangedTheList()
    {
        ConnectedPlayers players = new();
        ScannerRoomScenarioFixture scenario = new(playerManager: players.PlayerManager);
        Player simulator = players.Connect("Simulator");
        Player watcher = players.Connect("Watcher");
        scenario.StartScan(simulator, scenario.Room, quartz);
        await scenario.Manager.SetSubscriptionAsync(watcher, scenario.Room.Id, true);
        MapRoomScanTypesReport report = new(scenario.Room.Id, [titanium, quartz], [quartz, titanium], NitroxVector3.Zero, 300f);

        await scenario.Manager.ReportScanTypesAsync(simulator, report);
        // The simulating player's client reports the same list again every few seconds.
        await scenario.Manager.ReportScanTypesAsync(simulator, report);

        List<MapRoomScanTypesChanged> toSimulator = scenario.SentTo<MapRoomScanTypesChanged>(simulator);
        Assert.AreEqual(2, toSimulator.Count);
        CollectionAssert.AreEqual(new[] { quartz, titanium }, toSimulator[0].TechTypes);
        Assert.AreSame(toSimulator[0], scenario.SentTo<MapRoomScanTypesChanged>(watcher).Single());
        Assert.AreEqual(1, scenario.Diagnostics.GetHistory().Count(entry => entry.EventName == "scan_types"), "A report that changed nothing is not recorded.");
    }

    [TestMethod]
    public async Task ReportFromAPlayerWhoDoesNotSimulateTheRoomIsDropped()
    {
        ScannerRoomScenarioFixture scenario = new();
        scenario.StartScan(scenario.PlayerA, scenario.Room, quartz);

        IReadOnlyList<Packet> sent = await scenario.SentDuringAsync(() => scenario.Manager.ReportScanTypesAsync(scenario.PlayerB,
            new MapRoomScanTypesReport(scenario.Room.Id, [quartz], [quartz], NitroxVector3.Zero, 300f)));

        Assert.AreEqual(0, sent.Count);
        Assert.AreEqual(0, scenario.Manager.GetSnapshot(scenario.Room.Id)!.ScanTypeCount);
        Assert.AreEqual("non_owner", scenario.Diagnostics.GetHistory().Single(entry => entry.EventName == "scan_types").Reason);
    }

    private MapRoomScanTypesReport Report(List<NitroxTechType> types, List<NitroxTechType>? detectable = null) =>
        new(room.Id, types, detectable ?? [quartz, titanium], NitroxVector3.Zero, 300f);

    private static WorldEntity World(NitroxTechType techType, NitroxVector3 position) =>
        new(position, NitroxQuaternion.Identity, NitroxVector3.One, techType, 0, techType.ToString(), true, new NitroxId(), null);
}
