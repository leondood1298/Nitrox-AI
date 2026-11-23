using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Models.Packets.Processors;
using Nitrox.Test.Server.Subnautica;
using Packet = Nitrox.Model.Packets.Packet;

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
        ScannerRoomDiagnostics diagnostics = new(NullLogger<ScannerRoomDiagnostics>.Instance);
        MapRoomScanTypesSnapshotProcessor processor = new(scenario.EntityRegistry, scenario.Ownership, diagnostics);

        // The simulating client republishes the available scan types every few seconds, changed or not.
        for (int refresh = 0; refresh < 50; refresh++)
        {
            await processor.Process(new AuthProcessorContext(scenario.PlayerA, new DiscardingPacketSender()), ScanTypes(quartz));
        }
        await processor.Process(new AuthProcessorContext(scenario.PlayerA, new DiscardingPacketSender()), ScanTypes(quartz, titanium));

        diagnostics.GetHistory().Select(entry => $"{entry.EventName} {entry.Reason}").Should().Equal(
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
    public async Task ScanProgressFromAPlayerWhoDoesNotSimulateTheRoomIsThinnedOut()
    {
        ScannerRoomScenarioFixture scenario = new();
        scenario.Room.Metadata = new MapRoomMetadata(quartz, 5, 1, 1);
        CapturingLogger<ScannerRoomDiagnostics> diagnosticsLog = new();
        CapturingLogger<EntityMetadataUpdateProcessor> processorLog = new();
        ScannerRoomDiagnostics diagnostics = new(diagnosticsLog, scenario.Ownership);
        EntityMetadataUpdateProcessor processor = CreateMetadataProcessor(scenario, diagnostics, processorLog);

        // Nobody simulates the room, so every client keeps counting scanned nodes and reports each one.
        for (int node = 6; node < 46; node++)
        {
            await processor.Process(new AuthProcessorContext(scenario.PlayerB, new DiscardingPacketSender()),
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
        EntityMetadataUpdateProcessor processor = CreateMetadataProcessor(scenario, scenario.Diagnostics, new CapturingLogger<EntityMetadataUpdateProcessor>());

        // Player B still holds generation 2 (for instance from before the room was rebuilt) and picks another target.
        await processor.Process(new AuthProcessorContext(scenario.PlayerB, new DiscardingPacketSender()),
            new EntityMetadataUpdate(ScannerRoomScenarioFixture.RoomId, new MapRoomMetadata(titanium, 0, 2, 4)));

        ScannerRoomDiagnosticEntry entry = scenario.Diagnostics.GetHistory().Single();
        entry.Outcome.Should().Be(ScannerRoomDiagnosticOutcome.Rejected, "the player's choice was dropped; without a line nobody can tell why the scan never started");
        entry.EventName.Should().Be("scan_target");
        entry.SessionId.Should().Be(scenario.PlayerB.SessionId);
        entry.Reason.Should().Be("stale_request_gen_2_rev_4_room_gen_3_rev_7");
    }

    [TestMethod]
    public async Task RepeatedScanProgressFromTheSimulatingPlayerIsNotRecorded()
    {
        ScannerRoomScenarioFixture scenario = CreateScenarioWithOwnedRoom();
        scenario.Room.Metadata = new MapRoomMetadata(quartz, 5, 3, 7);
        EntityMetadataUpdateProcessor processor = CreateMetadataProcessor(scenario, scenario.Diagnostics, new CapturingLogger<EntityMetadataUpdateProcessor>());

        // The throttled progress report can repeat a count the server already has.
        await processor.Process(new AuthProcessorContext(scenario.PlayerA, new DiscardingPacketSender()),
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
        MapRoomScanResultSnapshotProcessor processor = new(scenario.EntityRegistry, scenario.Ownership,
            new PlayerManager(null!, null!, null!, NullLogger<PlayerManager>.Instance), new MapRoomScanResultSubscriptions(), scenario.Diagnostics);

        // The simulating player still scans for generation 4, as after the room it knew was replaced.
        await processor.Process(new AuthProcessorContext(scenario.PlayerA, new DiscardingPacketSender()),
            new MapRoomScanResultSnapshot(ScannerRoomScenarioFixture.RoomId, 4, [], NitroxVector3.Zero, 300f));

        ScannerRoomDiagnosticEntry entry = scenario.Diagnostics.GetHistory().Single();
        entry.Outcome.Should().Be(ScannerRoomDiagnosticOutcome.Rejected);
        entry.Reason.Should().Be("stale_or_invalid_gen_4_room_gen_7",
            "'stale or invalid' alone does not tell an outdated client from a malformed request");
    }

    private static EntityMetadataUpdateProcessor CreateMetadataProcessor(ScannerRoomScenarioFixture scenario, ScannerRoomDiagnostics diagnostics,
        ILogger<EntityMetadataUpdateProcessor> logger) =>
        new(new PlayerManager(null!, null!, null!, NullLogger<PlayerManager>.Instance), scenario.EntityRegistry, scenario.Ownership, diagnostics,
            new BasePowerSourceAuthority(), logger);

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

    private sealed class DiscardingPacketSender : IPacketSender
    {
        public ValueTask SendPacketAsync<T>(T packet, SessionId sessionId) where T : Packet => ValueTask.CompletedTask;

        public ValueTask SendPacketToAllAsync<T>(T packet) where T : Packet => ValueTask.CompletedTask;

        public ValueTask SendPacketToOthersAsync<T>(T packet, SessionId excludedSessionId) where T : Packet => ValueTask.CompletedTask;
    }
}
