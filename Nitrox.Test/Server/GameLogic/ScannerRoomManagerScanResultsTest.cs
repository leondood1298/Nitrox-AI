using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Test.Server.Helper;
using static Nitrox.Server.Subnautica.Models.GameLogic.ScannerRoomManager;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.GameLogic;

/// <summary>
///     What a room's scan has found: the rules for what the simulating player reports, and what the manager stores and
///     sends.
/// </summary>
[TestClass]
public sealed class ScannerRoomManagerScanResultsTest
{
    private const long GENERATION = 5;
    private static readonly NitroxTechType quartz = new("Quartz");
    private static readonly NitroxTechType titanium = new("Titanium");

    private readonly MapRoomEntity room = new(new NitroxId(), new NitroxId(), new NitroxInt3()) { Metadata = new MapRoomMetadata(quartz, 0, GENERATION) };
    private readonly RoomScan scan = new();

    [TestInitialize]
    public void Init() => scan.Begin(GENERATION);

    [TestMethod]
    public void AddUpdateRemoveMutateCanonicalResults()
    {
        Assert.AreEqual(ScanResultOutcome.CHANGED, Apply(Found("resource", 1f)));
        Assert.AreEqual(ScanResultOutcome.CHANGED, Apply(Found("resource", 2f)));
        Assert.AreEqual(1, scan.Results.Count);
        Assert.AreEqual(new NitroxVector3(2f, 0f, 0f), scan.Results[0].Position);
        Assert.AreEqual(ScanResultOutcome.CHANGED, Apply(Gone("resource")));
        Assert.AreEqual(0, scan.Results.Count);
    }

    [TestMethod]
    public void RejectsStaleGenerationAndWrongTargetAndIgnoresDuplicateReplay()
    {
        MapRoomScanResultReport accepted = Found("resource", 1f);
        Assert.AreEqual(ScanResultOutcome.CHANGED, Apply(accepted));

        Assert.AreEqual(ScanResultOutcome.UNCHANGED, Apply(accepted));
        Assert.AreEqual(ScanResultOutcome.REFUSED, Apply(Found("stale", 0f, generation: 4)));
        Assert.AreEqual(ScanResultOutcome.REFUSED, Apply(Report(new MapRoomScanResultRecord("wrong", titanium, NitroxVector3.Zero))));
        Assert.AreEqual(1, scan.Results.Count);
    }

    [TestMethod]
    public void RejectsEmptyIdAndWrongRoomAndIgnoresUnknownRemoval()
    {
        Assert.AreEqual(ScanResultOutcome.REFUSED, Apply(Found("", 1f)));
        Assert.AreEqual(ScanResultOutcome.REFUSED, Apply(new MapRoomScanResultReport(new NitroxId(), GENERATION,
            new MapRoomScanResultRecord("wrong-room", quartz, NitroxVector3.Zero), false, false, NitroxVector3.Zero, 300f)));
        Assert.AreEqual(ScanResultOutcome.UNCHANGED, Apply(Gone("missing")));
    }

    [TestMethod]
    public void RejectsOversizedIdsAndNonFinitePositions()
    {
        Assert.AreEqual(ScanResultOutcome.REFUSED, Apply(Found(new string('x', 257), 0f)));
        Assert.AreEqual(ScanResultOutcome.REFUSED, Apply(Report(new MapRoomScanResultRecord("nan", quartz, new NitroxVector3(float.NaN, 0f, 0f)))));
        Assert.AreEqual(ScanResultOutcome.REFUSED, Apply(Report(new MapRoomScanResultRecord("infinity", quartz, new NitroxVector3(0f, float.PositiveInfinity, 0f)))));
        Assert.AreEqual(ScanResultOutcome.CHANGED, Apply(Found("remove", 0f)));
        Assert.AreEqual(ScanResultOutcome.CHANGED, Apply(Report(new MapRoomScanResultRecord("remove", quartz, new NitroxVector3(float.NaN, 0f, 0f)), removed: true)));
        Assert.AreEqual(0, scan.Results.Count);
    }

    [TestMethod]
    public void FullReportReplacesResultsAndAReplayChangesNothing()
    {
        scan.TryApply(new MapRoomScanResultRecord("old", quartz, NitroxVector3.Zero));
        MapRoomScanReport report = FullReport(
        [
            new MapRoomScanResultRecord("first", quartz, new NitroxVector3(1f, 0f, 0f)),
            new MapRoomScanResultRecord("second", quartz, new NitroxVector3(2f, 0f, 0f))
        ]);

        Assert.IsTrue(TryApplyScanReport(room, scan, report, [], NitroxVector3.Zero, out bool changed));
        Assert.IsTrue(changed);
        CollectionAssert.AreEqual(new[] { "first", "second" }, scan.Results.Select(result => result.ResourceId).ToArray());

        Assert.IsTrue(TryApplyScanReport(room, scan, report, [], NitroxVector3.Zero, out bool changedAgain));
        Assert.IsFalse(changedAgain);
        CollectionAssert.AreEqual(new[] { "first", "second" }, scan.Results.Select(result => result.ResourceId).ToArray());
    }

    [TestMethod]
    public void FullReportRejectsDuplicatesWrongTargetAndStaleGenerationWithoutMutation()
    {
        List<MapRoomScanResultRecord> duplicate = [new("same", quartz, NitroxVector3.Zero), new("same", quartz, NitroxVector3.One)];
        List<MapRoomScanResultRecord> wrongTarget = [new("wrong", titanium, NitroxVector3.Zero)];

        Assert.IsFalse(TryApplyScanReport(room, scan, FullReport(duplicate), [], NitroxVector3.Zero, out _));
        Assert.IsFalse(TryApplyScanReport(room, scan, FullReport(wrongTarget), [], NitroxVector3.Zero, out _));
        Assert.IsFalse(TryApplyScanReport(room, scan, FullReport([], generation: 4), [], NitroxVector3.Zero, out _));
        Assert.AreEqual(0, scan.Results.Count);
    }

    [TestMethod]
    public void FullReportRejectsInvalidIdsAndPositionsWithoutPartialMutation()
    {
        List<MapRoomScanResultRecord> invalid =
        [
            new("valid", quartz, NitroxVector3.Zero),
            new(new string('x', 257), quartz, NitroxVector3.One),
            new("nan", quartz, new NitroxVector3(0f, 0f, float.NaN))
        ];

        Assert.IsFalse(TryApplyScanReport(room, scan, FullReport(invalid), [], NitroxVector3.Zero, out _));
        Assert.AreEqual(0, scan.Results.Count);
    }

    [TestMethod]
    public void AdditionRequiresValidatedInstalledRangeQuery()
    {
        MapRoomScanResultReport valid = Found("valid", 100f);

        Assert.AreEqual(ScanResultOutcome.REFUSED, ApplyScanResultReport(room, scan, valid, Registry(), null, out _));
        Assert.AreEqual(ScanResultOutcome.CHANGED, Apply(valid));
        Assert.AreEqual(ScanResultOutcome.REFUSED, Apply(Found("outside", 301f)));
        Assert.AreEqual(ScanResultOutcome.REFUSED, Apply(Report(new MapRoomScanResultRecord("remote", quartz, new NitroxVector3(600f, 0f, 0f)),
            scanOrigin: new NitroxVector3(513f, 0f, 0f))));
        Assert.AreEqual(ScanResultOutcome.REFUSED, Apply(Found("uninstalled-range", 340f, scanRange: 350f)));
        room.ChildEntities.Add(RangeUpgrade());
        Assert.AreEqual(ScanResultOutcome.CHANGED, Apply(Found("installed-range", 340f, scanRange: 350f)));
    }

    [TestMethod]
    public void AdditionCapRejectsNewIdButAllowsExistingUpdate()
    {
        scan.TryReplace(Enumerable.Range(0, MAX_SNAPSHOT_RESULTS)
                                  .Select(index => new MapRoomScanResultRecord($"resource-{index}", quartz, NitroxVector3.Zero))
                                  .ToList());

        Assert.AreEqual(ScanResultOutcome.REFUSED, Apply(Found("overflow", 1f)));
        Assert.AreEqual(ScanResultOutcome.CHANGED, Apply(Found("resource-0", 2f)));
        Assert.AreEqual(MAX_SNAPSHOT_RESULTS, scan.Results.Count);
        Assert.AreEqual(new NitroxVector3(2f, 0f, 0f), scan.Results[0].Position);
    }

    [TestMethod]
    public void ExactAdditionUsesAuthoritativeWorldPositionWhileOverrideUsesReportedPosition()
    {
        NitroxId exactId = new();
        EntityRegistry registry = Registry(World(exactId, quartz, new NitroxVector3(25f, 0f, 0f)));

        Assert.AreEqual(ScanResultOutcome.CHANGED, ApplyScanResultReport(room, scan, Found(exactId.ToString(), 900f), registry, NitroxVector3.Zero,
            out MapRoomScanResultRecord? accepted));
        Assert.AreEqual(new NitroxVector3(25f, 0f, 0f), scan.Results.Single().Position);
        Assert.AreEqual(new NitroxVector3(25f, 0f, 0f), accepted!.Position, "The subscribers are told the server's position, not the reported one.");

        NitroxId outsideId = new();
        registry.AddEntity(World(outsideId, quartz, new NitroxVector3(301f, 0f, 0f)));
        Assert.AreEqual(ScanResultOutcome.REFUSED, Apply(Found(outsideId.ToString(), 10f), registry));

        Assert.AreEqual(ScanResultOutcome.CHANGED, Apply(Found("override", 40f), registry));
        Assert.AreEqual(new NitroxVector3(40f, 0f, 0f), scan.Results.Single(result => result.ResourceId == "override").Position);
    }

    [TestMethod]
    public void TrackerUnloadCannotRemoveLiveExactResultButOwnerOnlyOverrideCanBeRemoved()
    {
        NitroxId exactId = new();
        scan.TryApply(new MapRoomScanResultRecord(exactId.ToString(), quartz, new NitroxVector3(10f, 0f, 0f)));
        EntityRegistry exactRegistry = Registry(World(exactId, quartz, new NitroxVector3(10f, 0f, 0f)));

        Assert.AreEqual(ScanResultOutcome.CORRECTION_REQUIRED, Apply(Gone(exactId.ToString(), 10f), exactRegistry, NitroxVector3.Zero));
        Assert.AreEqual(1, scan.Results.Count);

        MapRoomScanResultReport invalidQuery = Report(new MapRoomScanResultRecord(exactId.ToString(), quartz, new NitroxVector3(10f, 0f, 0f)), removed: true,
            scanOrigin: new NitroxVector3(513f, 0f, 0f));
        Assert.AreEqual(ScanResultOutcome.REFUSED, Apply(invalidQuery, exactRegistry, NitroxVector3.Zero), "An invalid request is not answered with the full list.");

        EntityRegistry overrideRegistry = Registry(World(exactId, titanium, new NitroxVector3(10f, 0f, 0f)));
        Assert.AreEqual(ScanResultOutcome.CHANGED, Apply(Gone(exactId.ToString(), 10f), overrideRegistry));
        Assert.AreEqual(0, scan.Results.Count);
    }

    [TestMethod]
    public void OrdinaryExactRemovalOutsideValidatedRangeRemovesCanonicalResult()
    {
        NitroxId exactId = new();
        scan.TryApply(new MapRoomScanResultRecord(exactId.ToString(), quartz, new NitroxVector3(10f, 0f, 0f)));
        EntityRegistry registry = Registry(World(exactId, quartz, new NitroxVector3(301f, 0f, 0f)));

        Assert.AreEqual(ScanResultOutcome.CHANGED, Apply(Gone(exactId.ToString(), 10f), registry, NitroxVector3.Zero));
        Assert.AreEqual(0, scan.Results.Count);
    }

    [TestMethod]
    public void RangeExitUsesAuthoritativeExactPositionAndRejectsForgedQuery()
    {
        NitroxId outsideId = new();
        scan.TryApply(new MapRoomScanResultRecord(outsideId.ToString(), quartz, new NitroxVector3(250f, 0f, 0f)));
        EntityRegistry outsideRegistry = Registry(World(outsideId, quartz, new NitroxVector3(301f, 0f, 0f)));

        Assert.AreEqual(ScanResultOutcome.CHANGED, Apply(RangeExit(outsideId.ToString(), new NitroxVector3(1f, 0f, 0f)), outsideRegistry, NitroxVector3.Zero));
        Assert.AreEqual(0, scan.Results.Count);

        NitroxId insideId = new();
        scan.TryApply(new MapRoomScanResultRecord(insideId.ToString(), quartz, new NitroxVector3(100f, 0f, 0f)));
        EntityRegistry insideRegistry = Registry(World(insideId, quartz, new NitroxVector3(100f, 0f, 0f)));
        Assert.AreEqual(ScanResultOutcome.CORRECTION_REQUIRED, Apply(RangeExit(insideId.ToString(), new NitroxVector3(301f, 0f, 0f)), insideRegistry, NitroxVector3.Zero));
        Assert.AreEqual(ScanResultOutcome.REFUSED, Apply(RangeExit(insideId.ToString(), new NitroxVector3(301f, 0f, 0f), scanOrigin: new NitroxVector3(513f, 0f, 0f)),
            insideRegistry, NitroxVector3.Zero));
        Assert.AreEqual(ScanResultOutcome.REFUSED, Apply(RangeExit(insideId.ToString(), new NitroxVector3(301f, 0f, 0f)), insideRegistry));
        Assert.AreEqual(1, scan.Results.Count);
    }

    [TestMethod]
    public void OwnerOnlyRangeExitUsesReportedPositionAndInstalledRange()
    {
        string overrideId = "override-result";
        scan.TryApply(new MapRoomScanResultRecord(overrideId, quartz, new NitroxVector3(250f, 0f, 0f)));

        Assert.AreEqual(ScanResultOutcome.REFUSED, Apply(RangeExit(overrideId, new NitroxVector3(100f, 0f, 0f)), scanAnchor: NitroxVector3.Zero));
        Assert.AreEqual(ScanResultOutcome.REFUSED, Apply(RangeExit(overrideId, new NitroxVector3(351f, 0f, 0f), scanRange: 350f), scanAnchor: NitroxVector3.Zero));
        Assert.AreEqual(ScanResultOutcome.CHANGED, Apply(RangeExit(overrideId, new NitroxVector3(301f, 0f, 0f)), scanAnchor: NitroxVector3.Zero));
        Assert.AreEqual(0, scan.Results.Count);
    }

    [TestMethod]
    public void FullReportMergesExactWorldTypesAndKeepsOwnerOnlyResults()
    {
        NitroxId exactId = new("00000000-0000-0000-0000-000000000001");
        List<MapRoomScanResultRecord> ownerResults = [new("override-result", quartz, new NitroxVector3(20f, 0f, 0f))];
        List<WorldEntity> worldEntities =
        [
            World(exactId, quartz, new NitroxVector3(10f, 0f, 0f)),
            World(new NitroxId(), titanium, new NitroxVector3(5f, 0f, 0f)),
            World(new NitroxId(), quartz, new NitroxVector3(301f, 0f, 0f))
        ];

        Assert.IsTrue(TryApplyScanReport(room, scan, FullReport(ownerResults), worldEntities, NitroxVector3.Zero, out _));

        CollectionAssert.AreEqual(new[] { exactId.ToString(), "override-result" }, scan.Results.Select(result => result.ResourceId).ToArray());
    }

    [TestMethod]
    public void StoppedScanNeverSupplementsWorldEntitiesWithNoneTechType()
    {
        room.Metadata = new MapRoomMetadata(NitroxTechType.None, 0, GENERATION);
        List<WorldEntity> worldEntities = [World(new NitroxId(), NitroxTechType.None, new NitroxVector3(10f, 0f, 0f))];

        Assert.IsTrue(TryApplyScanReport(room, scan, FullReport([]), worldEntities, NitroxVector3.Zero, out _));
        Assert.IsFalse(TryApplyScanReport(room, scan,
            FullReport([new MapRoomScanResultRecord("forged-none", NitroxTechType.None, new NitroxVector3(1f, 0f, 0f))]), worldEntities, NitroxVector3.Zero, out _));
        Assert.AreEqual(ScanResultOutcome.REFUSED, Apply(Report(new MapRoomScanResultRecord("delta-none", NitroxTechType.None, new NitroxVector3(1f, 0f, 0f)))));

        Assert.AreEqual(0, scan.Results.Count);
    }

    [TestMethod]
    public void FullReportRejectsInvalidQueryAndOwnerResultOutsideRange()
    {
        Assert.IsFalse(TryApplyScanReport(room, scan, new MapRoomScanReport(room.Id, GENERATION, new NitroxVector3(float.NaN, 0f, 0f), 300f, []), [], NitroxVector3.Zero, out _));
        Assert.IsFalse(TryApplyScanReport(room, scan, new MapRoomScanReport(room.Id, GENERATION, NitroxVector3.Zero, 299f, []), [], NitroxVector3.Zero, out _));
        Assert.IsFalse(TryApplyScanReport(room, scan, new MapRoomScanReport(room.Id, GENERATION, new NitroxVector3(513f, 0f, 0f), 300f, []), [], NitroxVector3.Zero, out _));
        Assert.IsFalse(TryApplyScanReport(room, scan, new MapRoomScanReport(room.Id, GENERATION, NitroxVector3.Zero, 350f, []), [], NitroxVector3.Zero, out _));
        room.ChildEntities.Add(RangeUpgrade());
        Assert.IsTrue(TryApplyScanReport(room, scan, new MapRoomScanReport(room.Id, GENERATION, NitroxVector3.Zero, 350.005f, []), [], NitroxVector3.Zero, out _));
        Assert.IsFalse(TryApplyScanReport(room, scan, new MapRoomScanReport(room.Id, GENERATION, NitroxVector3.Zero, 400f, []), [], NitroxVector3.Zero, out _));
        room.ChildEntities.AddRange([RangeUpgrade(), RangeUpgrade(), RangeUpgrade()]);
        Assert.IsTrue(TryApplyScanReport(room, scan, new MapRoomScanReport(room.Id, GENERATION, NitroxVector3.Zero, 500f, []), [], NitroxVector3.Zero, out _));
        Assert.IsFalse(TryApplyScanReport(room, scan,
            FullReport([new MapRoomScanResultRecord("outside", quartz, new NitroxVector3(301f, 0f, 0f))]), [], NitroxVector3.Zero, out _));
    }

    [TestMethod]
    public void NewTargetStartsWithNoResults()
    {
        scan.TryApply(new MapRoomScanResultRecord("found", quartz, NitroxVector3.Zero));

        scan.Begin(GENERATION);
        Assert.AreEqual(1, scan.Results.Count, "The same target keeps what was found for it.");
        scan.Begin(GENERATION + 1);

        Assert.AreEqual(0, scan.Results.Count);
        Assert.AreEqual(GENERATION + 1, scan.Generation);
    }

    [TestMethod]
    public async Task FullReportIsAnsweredWithTheMergedListAndPassedOnToSubscribers()
    {
        ConnectedPlayers players = new();
        ScannerRoomScenarioFixture scenario = new(playerManager: players.PlayerManager);
        ServerPlayer simulator = players.Connect("Simulator");
        ServerPlayer watcher = players.Connect("Watcher");
        ServerPlayer stranger = players.Connect("Stranger");
        NitroxId known = new("00000000-0000-0000-0000-000000000301");
        scenario.StartScan(simulator, scenario.Room, quartz);
        scenario.EntityRegistry.AddEntity(World(known, quartz, new NitroxVector3(10f, 0f, 0f)));
        await scenario.Manager.SetSubscriptionAsync(watcher, scenario.Room.Id, true);

        IReadOnlyList<Packet> sent = await scenario.ReportScanAsync(simulator, scenario.Room, new MapRoomScanResultRecord("only-the-simulator-has-it", quartz, new NitroxVector3(20f, 0f, 0f)));

        Assert.AreEqual(2, sent.Count);
        MapRoomScanSnapshot answer = scenario.SentTo<MapRoomScanSnapshot>(simulator).Single();
        Assert.AreEqual(GENERATION, answer.Generation);
        CollectionAssert.AreEqual(new[] { known.ToString(), "only-the-simulator-has-it" }, answer.Results.Select(result => result.ResourceId).ToArray(),
            "The simulating player is sent what the server added, which its own game has not loaded.");
        Assert.AreSame(answer, scenario.SentTo<MapRoomScanSnapshot>(watcher).Last());
        Assert.AreEqual(0, scenario.SentTo<MapRoomScanSnapshot>(stranger).Count, "A player who did not subscribe is sent nothing.");
        Assert.AreEqual(2, scenario.Manager.GetSnapshot(scenario.Room.Id)!.ScanResultCount);
    }

    [TestMethod]
    public async Task ReportThatChangesNothingIsOnlyAnsweredToItsSender()
    {
        ConnectedPlayers players = new();
        ScannerRoomScenarioFixture scenario = new(playerManager: players.PlayerManager);
        ServerPlayer simulator = players.Connect("Simulator");
        ServerPlayer watcher = players.Connect("Watcher");
        MapRoomScanResultRecord found = new("found", quartz, new NitroxVector3(20f, 0f, 0f));
        scenario.StartScan(simulator, scenario.Room, quartz);
        await scenario.ReportScanAsync(simulator, scenario.Room, found);
        await scenario.Manager.SetSubscriptionAsync(watcher, scenario.Room.Id, true);
        int watcherHad = scenario.SentTo<MapRoomScanSnapshot>(watcher).Count;

        IReadOnlyList<Packet> sent = await scenario.ReportScanAsync(simulator, scenario.Room, found);

        Assert.AreEqual(1, sent.Count, "The simulating player reports again whenever its own list was rebuilt.");
        Assert.AreEqual(2, scenario.SentTo<MapRoomScanSnapshot>(simulator).Count);
        Assert.AreEqual(watcherHad, scenario.SentTo<MapRoomScanSnapshot>(watcher).Count);
    }

    [TestMethod]
    public async Task ReportIsDroppedUnlessItComesFromTheSimulatingPlayerForTheCurrentTarget()
    {
        ScannerRoomScenarioFixture scenario = new();
        MapRoomScanResultRecord found = new("found", quartz, new NitroxVector3(20f, 0f, 0f));
        scenario.StartScan(scenario.PlayerA, scenario.Room, quartz);

        IReadOnlyList<Packet> fromAnotherPlayer = await scenario.ReportScanAsync(scenario.PlayerB, scenario.Room, found);
        IReadOnlyList<Packet> forAnOlderTarget = await scenario.SentDuringAsync(() => scenario.Manager.ReportScanAsync(scenario.PlayerA,
            new MapRoomScanReport(scenario.Room.Id, GENERATION - 1, NitroxVector3.Zero, 300f, [found])));
        IReadOnlyList<Packet> resultFromAnotherPlayer = await scenario.SentDuringAsync(() => scenario.Manager.ReportScanResultAsync(scenario.PlayerB,
            new MapRoomScanResultReport(scenario.Room.Id, GENERATION, found, false, false, NitroxVector3.Zero, 300f)));

        Assert.AreEqual(0, fromAnotherPlayer.Count + forAnOlderTarget.Count + resultFromAnotherPlayer.Count, "A refused report is not answered.");
        Assert.AreEqual(0, scenario.Manager.GetSnapshot(scenario.Room.Id)!.ScanResultCount);
        CollectionAssert.AreEqual(new[] { "non_owner", "stale_or_invalid_gen_4_room_gen_5", "result_non_owner" },
            scenario.Diagnostics.GetHistory().Where(entry => entry.EventName == "scan_snapshot").Select(entry => entry.Reason).ToArray());
    }

    [TestMethod]
    public async Task AcceptedResultGoesToTheSimulatingPlayerAndTheOtherSubscribers()
    {
        ConnectedPlayers players = new();
        ScannerRoomScenarioFixture scenario = new(playerManager: players.PlayerManager);
        ServerPlayer simulator = players.Connect("Simulator");
        ServerPlayer watcher = players.Connect("Watcher");
        scenario.StartScan(simulator, scenario.Room, quartz);
        await scenario.Manager.SetSubscriptionAsync(simulator, scenario.Room.Id, true);
        await scenario.Manager.SetSubscriptionAsync(watcher, scenario.Room.Id, true);

        await scenario.Manager.ReportScanResultAsync(simulator, new MapRoomScanResultReport(scenario.Room.Id, GENERATION,
            new MapRoomScanResultRecord("found", quartz, new NitroxVector3(20f, 0f, 0f)), false, false, NitroxVector3.Zero, 300f));

        MapRoomScanResultChanged toSimulator = scenario.SentTo<MapRoomScanResultChanged>(simulator).Single();
        Assert.AreEqual("found", toSimulator.Result.ResourceId);
        Assert.IsFalse(toSimulator.Removed);
        Assert.AreEqual(GENERATION, toSimulator.Generation);
        Assert.AreSame(toSimulator, scenario.SentTo<MapRoomScanResultChanged>(watcher).Single());
    }

    [TestMethod]
    public async Task UnloadOfAResourceTheServerStillHasInRangeIsAnsweredWithTheFullList()
    {
        ScannerRoomScenarioFixture scenario = new();
        NitroxId resourceId = new("00000000-0000-0000-0000-000000000301");
        scenario.StartScan(scenario.PlayerA, scenario.Room, quartz);
        scenario.EntityRegistry.AddEntity(World(resourceId, quartz, new NitroxVector3(10f, 0f, 0f)));
        await scenario.ReportScanAsync(scenario.PlayerA, scenario.Room);

        IReadOnlyList<Packet> replies = await scenario.SentDuringAsync(() => scenario.Manager.ReportScanResultAsync(scenario.PlayerA,
            new MapRoomScanResultReport(scenario.Room.Id, GENERATION, new MapRoomScanResultRecord(resourceId.ToString(), quartz, new NitroxVector3(10f, 0f, 0f)), true, false,
                NitroxVector3.Zero, 300f)));

        MapRoomScanSnapshot correction = (MapRoomScanSnapshot)replies.Single();
        Assert.AreEqual(GENERATION, correction.Generation);
        CollectionAssert.AreEqual(new[] { resourceId.ToString() }, correction.Results.Select(result => result.ResourceId).ToArray());
        Assert.AreEqual(1, scenario.Manager.GetSnapshot(scenario.Room.Id)!.ScanResultCount);
    }

    [TestMethod]
    public async Task RemovalOfAResourceTheServerHasOutOfRangeIsPassedOn()
    {
        ScannerRoomScenarioFixture scenario = new();
        MapRoomScanResultRecord reported = new("00000000-0000-0000-0000-000000000302", quartz, new NitroxVector3(10f, 0f, 0f));
        scenario.StartScan(scenario.PlayerA, scenario.Room, quartz);
        await scenario.Manager.ReportScanResultAsync(scenario.PlayerA, new MapRoomScanResultReport(scenario.Room.Id, GENERATION, reported, false, false, NitroxVector3.Zero, 300f));
        scenario.EntityRegistry.AddEntity(World(new NitroxId(reported.ResourceId), quartz, new NitroxVector3(301f, 0f, 0f)));

        IReadOnlyList<Packet> replies = await scenario.SentDuringAsync(() => scenario.Manager.ReportScanResultAsync(scenario.PlayerA,
            new MapRoomScanResultReport(scenario.Room.Id, GENERATION, reported, true, false, NitroxVector3.Zero, 300f)));

        MapRoomScanResultChanged accepted = (MapRoomScanResultChanged)replies.Single();
        Assert.IsTrue(accepted.Removed);
        Assert.AreEqual(reported.ResourceId, accepted.Result.ResourceId);
        Assert.AreEqual(0, scenario.Manager.GetSnapshot(scenario.Room.Id)!.ScanResultCount);
    }

    [TestMethod]
    public async Task RangeExitOfAResourceTheServerHasInRangeIsAnsweredWithTheFullList()
    {
        ScannerRoomScenarioFixture scenario = new();
        NitroxId resourceId = new("00000000-0000-0000-0000-000000000303");
        scenario.StartScan(scenario.PlayerA, scenario.Room, quartz);
        scenario.EntityRegistry.AddEntity(World(resourceId, quartz, new NitroxVector3(100f, 0f, 0f)));
        await scenario.ReportScanAsync(scenario.PlayerA, scenario.Room);

        IReadOnlyList<Packet> replies = await scenario.SentDuringAsync(() => scenario.Manager.ReportScanResultAsync(scenario.PlayerA,
            new MapRoomScanResultReport(scenario.Room.Id, GENERATION, new MapRoomScanResultRecord(resourceId.ToString(), quartz, new NitroxVector3(301f, 0f, 0f)), true, true,
                NitroxVector3.Zero, 300f)));

        MapRoomScanSnapshot correction = (MapRoomScanSnapshot)replies.Single();
        CollectionAssert.AreEqual(new[] { resourceId.ToString() }, correction.Results.Select(result => result.ResourceId).ToArray());
    }

    [TestMethod]
    public async Task EntityRemovalTakesTheResultOutOfEveryRoomThatFoundIt()
    {
        ScannerRoomScenarioFixture scenario = new();
        MapRoomEntity second = scenario.AddRoom(new MapRoomEntity(new NitroxId("00000000-0000-0000-0000-000000000110"), ScannerRoomScenarioFixture.RoomParentId, new NitroxInt3(5, -2, 9)));
        NitroxId resourceId = new("00000000-0000-0000-0000-000000000304");
        scenario.EntityRegistry.AddEntity(World(resourceId, quartz, new NitroxVector3(10f, 0f, 0f)));
        scenario.StartScan(scenario.PlayerA, scenario.Room, quartz);
        scenario.StartScan(scenario.PlayerA, second, quartz);
        await scenario.ReportScanAsync(scenario.PlayerA, scenario.Room, new MapRoomScanResultRecord("other", quartz, NitroxVector3.Zero));
        await scenario.ReportScanAsync(scenario.PlayerA, second);
        Assert.AreEqual(2, scenario.Manager.GetSnapshot(scenario.Room.Id)!.ScanResultCount);

        await scenario.Manager.EntityRemovedAsync(scenario.PlayerA, new NitroxId());
        Assert.AreEqual(2, scenario.Manager.GetSnapshot(scenario.Room.Id)!.ScanResultCount, "An entity no room has found changes nothing.");
        await scenario.Manager.EntityRemovedAsync(scenario.PlayerA, resourceId);

        Assert.AreEqual(1, scenario.Manager.GetSnapshot(scenario.Room.Id)!.ScanResultCount);
        Assert.AreEqual(0, scenario.Manager.GetSnapshot(second.Id)!.ScanResultCount);
    }

    private ScanResultOutcome Apply(MapRoomScanResultReport report, EntityRegistry? registry = null, NitroxVector3? scanAnchor = null) =>
        ApplyScanResultReport(room, scan, report, registry ?? Registry(), scanAnchor ?? (!report.Removed ? NitroxVector3.Zero : null), out _);

    private MapRoomScanResultReport Found(string resourceId, float x, long generation = GENERATION, float scanRange = 300f) =>
        new(room.Id, generation, new MapRoomScanResultRecord(resourceId, quartz, new NitroxVector3(x, 0f, 0f)), false, false, NitroxVector3.Zero, scanRange);

    private MapRoomScanResultReport Gone(string resourceId, float x = 0f) => Report(new MapRoomScanResultRecord(resourceId, quartz, new NitroxVector3(x, 0f, 0f)), removed: true);

    private MapRoomScanResultReport RangeExit(string resourceId, NitroxVector3 position, NitroxVector3? scanOrigin = null, float scanRange = 300f) =>
        new(room.Id, GENERATION, new MapRoomScanResultRecord(resourceId, quartz, position), true, true, scanOrigin ?? NitroxVector3.Zero, scanRange);

    private MapRoomScanResultReport Report(MapRoomScanResultRecord result, bool removed = false, NitroxVector3? scanOrigin = null) =>
        new(room.Id, GENERATION, result, removed, false, scanOrigin ?? NitroxVector3.Zero, 300f);

    private MapRoomScanReport FullReport(List<MapRoomScanResultRecord> results, long generation = GENERATION) => new(room.Id, generation, NitroxVector3.Zero, 300f, results);

    private static EntityRegistry Registry(params WorldEntity[] entities)
    {
        EntityRegistry registry = new(NullLogger<EntityRegistry>.Instance);
        registry.AddEntities(entities);
        return registry;
    }

    private static WorldEntity World(NitroxId id, NitroxTechType techType, NitroxVector3 position) =>
        new(position, NitroxQuaternion.Identity, NitroxVector3.One, techType, 0, id.ToString(), true, id, null);

    private static WorldEntity RangeUpgrade() => World(new NitroxId(), new NitroxTechType("MapRoomUpgradeScanRange"), NitroxVector3.Zero);
}
