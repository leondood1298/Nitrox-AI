using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Processors;
using Nitrox.Test.Server.Subnautica;

namespace Nitrox.Test.Server.GameLogic;

/// <summary>
///     Checks what the real packet processors make the Scanner Room diagnostics write, as opposed to
///     <see cref="ScannerRoomDiagnosticsTest" />, which checks the diagnostics on their own.
/// </summary>
[TestClass]
public sealed class ScannerRoomDiagnosticsCallSiteTest
{
    private static readonly NitroxTechType quartz = new("Quartz");
    private static readonly NitroxTechType titanium = new("Titanium");

    [TestMethod]
    public async Task UnchangedScanTypeRefreshesDoNotFillTheLog()
    {
        ScannerRoomScenarioFixture scenario = CreateScenarioWithOwnedRoom();
        MapRoomScanTypesSnapshotProcessor processor = new(scenario.Manager);

        // The simulating client republishes the available scan types every few seconds, changed or not.
        for (int refresh = 0; refresh < 50; refresh++)
        {
            await processor.Process(scenario.Context(scenario.PlayerA), ScanTypes(quartz));
        }
        await processor.Process(scenario.Context(scenario.PlayerA), ScanTypes(quartz, titanium));

        scenario.Diagnostics.GetHistory().Select(entry => $"{entry.EventName} {entry.Reason}").Should().Equal(
            ["scan_types types_1", "scan_types types_2"],
            "a refresh that changes nothing is not a transition; 49 of them would push every real one out of the history");
    }

    [TestMethod]
    public async Task FirstDockOfACameraTheServerDidNotKnowIsNamedAsSuch()
    {
        ScannerRoomScenarioFixture scenario = CreateScenarioWithOwnedRoom();
        NitroxId restoredCameraId = new();

        // A camera the game restored into a dock without the server ever having an entity or record for it.
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, restoredCameraId, 0, true)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraBId, 1, true)).Granted);

        scenario.Diagnostics.GetHistory().Select(entry => $"{entry.EventName} {entry.Reason}").Should().Equal(
            ["dock bootstrap", "dock changed"],
            "the first registration of an unknown camera is the step that goes wrong after a restart, so it must be told apart from an ordinary dock");
    }

    [TestMethod]
    public async Task TakingOverACameraLockedWithoutAPilotIsRecorded()
    {
        ScannerRoomScenarioFixture scenario = new();
        NitroxId cameraId = ScannerRoomScenarioFixture.CameraCId;
        ScannerRoomDiagnostics diagnostics = scenario.Diagnostics;
        // Player B's client took the generic exclusive lock for a Stalker carrying the camera and never gave it back.
        Assert.IsTrue(scenario.Ownership.TryAcquire(cameraId, scenario.PlayerB, SimulationLockType.EXCLUSIVE));

        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerA, cameraId, true)).Granted);

        diagnostics.GetHistory().Select(entry => $"{entry.EventName} sid={entry.SessionId} {entry.Reason}").Should().Equal(
            ["control_preempt sid=2 exclusive_lock_taken_by_1", "control_acquire sid=1 -"],
            "the player who lost the lock is told by a plain ownership change; only this line says why");
    }

    [TestMethod]
    public async Task PilotsSentToAJoinedPlayerAreRecorded()
    {
        ScannerRoomScenarioFixture scenario = new();
        NitroxId cameraId = ScannerRoomScenarioFixture.CameraCId;
        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerA, cameraId, true)).Granted);

        IReadOnlyList<MapRoomCameraControl> grants = await scenario.JoinedPlayerControlGrantsAsync(scenario.PlayerB.SessionId);

        grants.Should().ContainSingle();
        ScannerRoomDiagnosticEntry entry = scenario.Diagnostics.GetHistory()[^1];
        entry.Format().Should().Contain(" sid=1 ev=control_sync out=ok ").And.EndWith(" reason=current_pilot_sent_to_joined_player");
    }

    [TestMethod]
    public async Task ScanProgressFromAPlayerWhoDoesNotSimulateTheRoomIsThinnedOut()
    {
        CapturingLogger<ScannerRoomDiagnostics> diagnosticsLog = new();
        CapturingLogger<EntityMetadataUpdateProcessor> processorLog = new();
        ScannerRoomScenarioFixture scenario = new(diagnosticsLog: diagnosticsLog);
        scenario.Room.Metadata = new MapRoomMetadata(quartz, 5, 1, 1);
        ScannerRoomDiagnostics diagnostics = scenario.Diagnostics;
        EntityMetadataUpdateProcessor processor = CreateMetadataProcessor(scenario, processorLog);

        // Nobody simulates the room, so every client keeps counting scanned nodes and reports each one.
        for (int node = 6; node < 46; node++)
        {
            await processor.Process(scenario.Context(scenario.PlayerB),
                new EntityMetadataUpdate(ScannerRoomScenarioFixture.RoomId, new MapRoomMetadata(quartz, node, 1, 1)));
        }

        string[] warnings = processorLog.Entries.Concat(diagnosticsLog.Entries).Where(entry => entry.Level == LogLevel.Warning)
                                        .Select(entry => entry.Message).ToArray();
        warnings.Should().HaveCountLessThan(10, "one warning per scanned node buries everything else");
        warnings[0].Should().Contain(" sid=2 ev=scan_progress out=reject ").And.Contain(" roomLock=none ").And.EndWith(" reason=non_owner");
        diagnostics.GetCounters().Rejected.Should().Be(40);
    }

    [TestMethod]
    public async Task ScanTargetChangeBasedOnAnOutdatedStateIsRecorded()
    {
        ScannerRoomScenarioFixture scenario = CreateScenarioWithOwnedRoom();
        scenario.Room.Metadata = new MapRoomMetadata(quartz, 5, 3, 7);
        EntityMetadataUpdateProcessor processor = CreateMetadataProcessor(scenario, new CapturingLogger<EntityMetadataUpdateProcessor>());

        // Player B still holds generation 2 (for instance from before the room was rebuilt) and picks another target.
        await processor.Process(scenario.Context(scenario.PlayerB),
            new EntityMetadataUpdate(ScannerRoomScenarioFixture.RoomId, new MapRoomMetadata(titanium, 0, 2, 4)));

        ScannerRoomDiagnosticEntry entry = scenario.Diagnostics.GetHistory().Single();
        entry.Format().Should().Contain(" ev=scan_target out=reject ", "the player's choice was dropped; without a line nobody can tell why the scan never started");
        entry.SessionId.Should().Be(scenario.PlayerB.SessionId);
        entry.Reason.Should().Be("stale_request_gen_2_rev_4_room_gen_3_rev_7");
    }

    [TestMethod]
    public async Task RepeatedScanProgressFromTheSimulatingPlayerIsNotRecorded()
    {
        ScannerRoomScenarioFixture scenario = CreateScenarioWithOwnedRoom();
        scenario.Room.Metadata = new MapRoomMetadata(quartz, 5, 3, 7);
        EntityMetadataUpdateProcessor processor = CreateMetadataProcessor(scenario, new CapturingLogger<EntityMetadataUpdateProcessor>());

        // The throttled progress report can repeat a count the server already has.
        await processor.Process(scenario.Context(scenario.PlayerA),
            new EntityMetadataUpdate(ScannerRoomScenarioFixture.RoomId, new MapRoomMetadata(quartz, 5, 3, 7)));

        scenario.Diagnostics.GetHistory().Should().BeEmpty();
        scenario.Diagnostics.GetCounters().Rejected.Should().Be(0);
    }

    [TestMethod]
    public async Task RejectedScanSnapshotSaysWhichGenerationsDisagreed()
    {
        ScannerRoomScenarioFixture scenario = CreateScenarioWithOwnedRoom();
        scenario.Room.Metadata = new MapRoomMetadata(quartz, 0, 7, 9);
        scenario.Room.ScanResultGeneration = 7;
        MapRoomScanResultSnapshotProcessor processor = new(scenario.Manager);

        // The simulating player still scans for generation 4, as after the room it knew was replaced.
        await processor.Process(scenario.Context(scenario.PlayerA),
            new MapRoomScanResultSnapshot(ScannerRoomScenarioFixture.RoomId, 4, [], NitroxVector3.Zero, 300f));

        ScannerRoomDiagnosticEntry entry = scenario.Diagnostics.GetHistory().Single();
        entry.Format().Should().Contain(" ev=scan_snapshot out=reject ");
        entry.Reason.Should().Be("stale_or_invalid_gen_4_room_gen_7",
            "'stale or invalid' alone does not tell an outdated client from a malformed request");
    }

    private static EntityMetadataUpdateProcessor CreateMetadataProcessor(ScannerRoomScenarioFixture scenario, ILogger<EntityMetadataUpdateProcessor> logger) =>
        new(scenario.PlayerManager, scenario.EntityRegistry, scenario.Ownership, scenario.Manager,
            new BasePowerManager(scenario.EntityRegistry, scenario.Ownership, new BasePowerDiagnostics(), NullLogger<BasePowerManager>.Instance), logger);

    private static ScannerRoomScenarioFixture CreateScenarioWithOwnedRoom()
    {
        ScannerRoomScenarioFixture scenario = new();
        // The scan anchor is the position of the base the room is part of.
        scenario.EntityRegistry.AddEntity(new GlobalRootEntity(new NitroxTransform(), GlobalRootEntity.GLOBAL_ROOT_LEVEL, "base", true,
            ScannerRoomScenarioFixture.RoomParentId, new NitroxTechType("Base"), null, null, []));
        Assert.IsTrue(scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.RoomId, scenario.PlayerA, SimulationLockType.TRANSIENT));
        return scenario;
    }

    private static MapRoomScanTypesSnapshot ScanTypes(params NitroxTechType[] techTypes) =>
        new(ScannerRoomScenarioFixture.RoomId, [.. techTypes], [.. techTypes], NitroxVector3.Zero, 300f);

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception)));
    }
}
