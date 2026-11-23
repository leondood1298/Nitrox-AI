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
        MapRoomScanTypesReportProcessor processor = new(scenario.Manager);

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
    public async Task DocksUndocksAndRefusalsAreRecordedWithTheirDockAndWhatTheRoomHoldsAfterwards()
    {
        ScannerRoomScenarioFixture scenario = CreateScenarioWithOwnedRoom();

        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        Assert.IsFalse((await scenario.DockAsync(scenario.PlayerB, ScannerRoomScenarioFixture.CameraBId, 0, true)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerB, ScannerRoomScenarioFixture.CameraBId, 1, true)).Granted);
        Assert.IsFalse((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraBId, 1, false, establishAuthority: false)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, false)).Granted);

        scenario.Diagnostics.GetHistory().Select(entry => $"{entry.EventName} {entry.Outcome} sid={entry.SessionId} slot={entry.Slot} cams={entry.CameraCount} {entry.Reason}").Should().Equal(
            "dock ACCEPTED sid=1 slot=0 cams=1 changed",
            "dock ACCEPTED sid=1 slot=0 cams=1 idempotent",
            "dock REJECTED sid=2 slot=0 cams=1 slot_conflict",
            "dock ACCEPTED sid=2 slot=1 cams=2 changed",
            "undock REJECTED sid=1 slot=1 cams=2 non_owner",
            "undock ACCEPTED sid=1 slot=0 cams=1 changed");
        scenario.Diagnostics.GetHistory().Should().OnlyContain(entry => entry.RoomId == ScannerRoomScenarioFixture.RoomId.ToString().Substring(0, 8));
    }

    [TestMethod]
    public async Task LightBatteryAndHealthOfADockedCameraAreRecordedAgainstItsRoom()
    {
        ScannerRoomScenarioFixture scenario = CreateScenarioWithOwnedRoom();
        NitroxId cameraId = ScannerRoomScenarioFixture.CameraAId;
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 1, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);

        await scenario.ReportCameraAsync(scenario.PlayerA, cameraId, lightOn: true);
        await scenario.ReportCameraAsync(scenario.PlayerB, cameraId, lightOn: false);
        await scenario.ReportCameraAsync(scenario.PlayerA, cameraId, energy: 99f);
        await scenario.ReportCameraAsync(scenario.PlayerA, cameraId, energy: 20f);
        await scenario.ReportCameraAsync(scenario.PlayerA, cameraId, health: 0f);
        await scenario.ReportCameraAsync(scenario.PlayerB, cameraId, health: 50f);

        scenario.Diagnostics.GetHistory().Skip(2).Select(entry => $"{entry.EventName} {entry.Outcome} sid={entry.SessionId} {entry.Reason}").Should().Equal(
            ["light ACCEPTED sid=1 on", "light REJECTED sid=2 non_owner", "component_energy ACCEPTED sid=1 band_25", "component_health ACCEPTED sid=1 death", "component REJECTED sid=2 non_owner"],
            "a battery that drains within its band is not worth a line; a band it crosses, a death and every refusal are");
        scenario.Diagnostics.GetHistory().Skip(2).Should().OnlyContain(entry => entry.RoomId == ScannerRoomScenarioFixture.RoomId.ToString().Substring(0, 8));
    }

    [TestMethod]
    public async Task TakingOverACameraLockedWithoutAPilotIsRecorded()
    {
        ScannerRoomScenarioFixture scenario = new();
        NitroxId cameraId = ScannerRoomScenarioFixture.CameraCId;
        ScannerRoomDiagnostics diagnostics = scenario.Diagnostics;
        // Player B's client took the generic exclusive lock for a Stalker carrying the camera and never gave it back.
        Assert.IsTrue(scenario.Ownership.TryAcquire(cameraId, scenario.PlayerB, SimulationLockType.EXCLUSIVE));

        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);

        diagnostics.GetHistory().Select(entry => $"{entry.EventName} sid={entry.SessionId} {entry.Reason}").Should().Equal(
            ["control_preempt sid=2 exclusive_lock_taken_by_1", "control_acquire sid=1 -"],
            "the player who lost the lock is told by a plain ownership change; only this line says why");
    }

    [TestMethod]
    public async Task TakingBeingRefusedAndGivingUpACameraAreRecordedWithItsDock()
    {
        ScannerRoomScenarioFixture scenario = new();
        NitroxId cameraId = ScannerRoomScenarioFixture.CameraAId;
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);

        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);
        Assert.IsFalse((await scenario.ControlAsync(scenario.PlayerB, cameraId, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, false)).Granted);

        scenario.Diagnostics.GetHistory().Skip(1).Select(entry => $"{entry.EventName} {entry.Outcome} sid={entry.SessionId} slot={entry.Slot} {entry.Reason}").Should().Equal(
            ["control_acquire ACCEPTED sid=1 slot=0 -", "control_acquire REJECTED sid=2 slot=0 locked", "control_release ACCEPTED sid=1 slot=0 -"],
            "the lock is asked for like the lock on any entity; only these lines say that it was for a camera");
        scenario.Diagnostics.GetHistory().Should().OnlyContain(entry => entry.RoomId == ScannerRoomScenarioFixture.RoomId.ToString().Substring(0, 8));
    }

    [TestMethod]
    public async Task PilotsSentToAJoinedPlayerAreRecorded()
    {
        ScannerRoomScenarioFixture scenario = new();
        NitroxId cameraId = ScannerRoomScenarioFixture.CameraCId;
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);

        IReadOnlyList<Nitrox.Model.Packets.Packet> grants = await scenario.JoinedPlayerControlGrantsAsync(scenario.PlayerB.SessionId);

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
        scenario.Room.Metadata = new MapRoomMetadata(quartz, 5, 1);
        ScannerRoomDiagnostics diagnostics = scenario.Diagnostics;
        EntityMetadataUpdateProcessor processor = CreateMetadataProcessor(scenario, processorLog);

        // Nobody simulates the room, so every client keeps counting scanned nodes and reports each one.
        for (int node = 6; node < 46; node++)
        {
            await processor.Process(scenario.Context(scenario.PlayerB),
                new EntityMetadataUpdate(ScannerRoomScenarioFixture.RoomId, new MapRoomMetadata(quartz, node, 1)));
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
        scenario.Room.Metadata = new MapRoomMetadata(quartz, 5, 3);
        EntityMetadataUpdateProcessor processor = CreateMetadataProcessor(scenario, new CapturingLogger<EntityMetadataUpdateProcessor>());

        // Player B still holds generation 2 (another player's target has not reached it yet) and picks another target.
        await processor.Process(scenario.Context(scenario.PlayerB),
            new EntityMetadataUpdate(ScannerRoomScenarioFixture.RoomId, new MapRoomMetadata(titanium, 0, 2)));

        ScannerRoomDiagnosticEntry entry = scenario.Diagnostics.GetHistory().Single();
        entry.Format().Should().Contain(" ev=scan_target out=reject ", "the player's choice was dropped; without a line nobody can tell why the scan never started");
        entry.SessionId.Should().Be(scenario.PlayerB.SessionId);
        entry.Reason.Should().Be("stale_request_gen_2_room_gen_3");
    }

    [TestMethod]
    public async Task RepeatedScanProgressFromTheSimulatingPlayerIsNotRecorded()
    {
        ScannerRoomScenarioFixture scenario = CreateScenarioWithOwnedRoom();
        scenario.Room.Metadata = new MapRoomMetadata(quartz, 5, 3);
        EntityMetadataUpdateProcessor processor = CreateMetadataProcessor(scenario, new CapturingLogger<EntityMetadataUpdateProcessor>());

        // The throttled progress report can repeat a count the server already has.
        await processor.Process(scenario.Context(scenario.PlayerA),
            new EntityMetadataUpdate(ScannerRoomScenarioFixture.RoomId, new MapRoomMetadata(quartz, 5, 3)));

        scenario.Diagnostics.GetHistory().Should().BeEmpty();
        scenario.Diagnostics.GetCounters().Rejected.Should().Be(0);
    }

    [TestMethod]
    public async Task RejectedScanReportSaysWhichGenerationsDisagreed()
    {
        ScannerRoomScenarioFixture scenario = CreateScenarioWithOwnedRoom();
        scenario.Room.Metadata = new MapRoomMetadata(quartz, 0, 7);
        MapRoomScanReportProcessor processor = new(scenario.Manager);

        // The simulating player still scans for generation 4, as after the room it knew was replaced.
        await processor.Process(scenario.Context(scenario.PlayerA),
            new MapRoomScanReport(ScannerRoomScenarioFixture.RoomId, 4, NitroxVector3.Zero, 300f, []));

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

    private static MapRoomScanTypesReport ScanTypes(params NitroxTechType[] techTypes) =>
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
