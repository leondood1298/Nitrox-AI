using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Extensions;
using Nitrox.Server.Subnautica.Models.Commands;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Test.Server.Subnautica;

namespace Nitrox.Test.Server.Commands;

/// <summary>
///     The <c>scannerroom</c>, <c>scannermark</c> and <c>basepower</c> commands are what a tester runs on the server
///     console during a session, so their output has to state totals and anomalies before the listings.
/// </summary>
[TestClass]
public sealed class DiagnosticsCommandsTest
{
    [TestMethod]
    public async Task ScannerRoomCommandOnlyReads()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraBId, 1, true)).Granted);
        // Two cameras in one dock, which the server never stores: the command has to say so and still only read.
        scenario.SetCameraState(ScannerRoomScenarioFixture.CameraBId, scenario.CameraState(ScannerRoomScenarioFixture.CameraBId).WithDockingIndex(0));
        ScannerRoomCommand command = CreateScannerRoomCommand(scenario, scenario.Diagnostics);
        ScannerRoomDiagnosticCounters countersBefore = scenario.Diagnostics.GetCounters();

        string first = await RunAsync(command);
        string second = await RunAsync(command);

        scenario.Diagnostics.GetCounters().Should().Be(countersBefore, "a command that lists state must not change what it lists");
        second.Should().Be(first);
        first.Should().Contain(" ! room 00000000 breaks an invariant: duplicate_dock");
    }

    [TestMethod]
    public async Task ScannerRoomCommandStatesTotalsAndAnomaliesBeforeTheListing()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        // Camera B is held exclusively without anyone piloting it, as after a Stalker grab that never ended.
        Assert.IsTrue(scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.CameraBId, scenario.PlayerB, SimulationLockType.EXCLUSIVE));
        ScannerRoomDiagnostics diagnostics = new(NullLogger<ScannerRoomDiagnostics>.Instance, scenario.Ownership);

        string[] lines = (await RunAsync(CreateScannerRoomCommand(scenario, diagnostics))).Split(Environment.NewLine);

        lines[0].Should().StartWith($"Scanner Room diagnostics ep={diagnostics.Epoch}: rooms=1, cameras=3 (docked=1, loose=2), events=0 ");
        lines[1].Should().Be("Anomalies (3):");
        lines[2].Should().StartWith(" ! room 00000000 has no simulating player");
        lines.Should().Contain(line => line.StartsWith(" ! camera ") && line.Contains("is locked exclusively by B #2 who is not piloting it"));
        lines.Should().Contain(line => line.StartsWith(" ! camera ") && line.Contains("is out in the world and nobody simulates it"));
        Array.FindIndex(lines, line => line.StartsWith("Rooms and their cameras")).Should().Be(5, "the listing comes after every anomaly");
        lines[6].Should().StartWith($"[SRD1] snapshot ep={diagnostics.Epoch} side=S room={ScannerRoomScenarioFixture.RoomId} roomLock=none left=00000000.0000 right=- cams=1 ");
        lines[7].Should().StartWith("  cam=00000000.0000 number=1 where=slot0 camLock=1t control=- light=off energy=100.0 health=400.0 ")
                .And.EndWith($" id={ScannerRoomScenarioFixture.CameraAId}");
        lines[8].Should().Be("Cameras in no dock (2):");
        lines[9].Should().Contain(" where=world camLock=2x control=- ").And.EndWith($" id={ScannerRoomScenarioFixture.CameraBId}");
        lines[10].Should().Contain(" where=world camLock=none control=- ").And.EndWith($" id={ScannerRoomScenarioFixture.CameraCId}");
    }

    [TestMethod]
    public async Task ScannerRoomCommandShowsWhoPilotsACamera()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue(scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.RoomId, scenario.PlayerA, SimulationLockType.TRANSIENT));
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        ScannerRoomDiagnostics diagnostics = new(NullLogger<ScannerRoomDiagnostics>.Instance, scenario.Ownership);

        string output = await RunAsync(CreateScannerRoomCommand(scenario, diagnostics));

        output.Should().Contain(" roomLock=1t left=00000000.0000 ");
        output.Should().Contain(" where=slot0 camLock=2x control=2 ");
        output.Should().NotContain("who is not piloting it");
    }

    [TestMethod]
    public async Task ScannerRoomCommandFoldsRepeatedTransitions()
    {
        ScannerRoomScenarioFixture scenario = new();
        for (int index = 0; index < 40; index++)
        {
            scenario.Diagnostics.RecordRejected("component", scenario.Room, ScannerRoomScenarioFixture.CameraAId, scenario.PlayerB.SessionId, reason: "non_owner");
        }
        scenario.Diagnostics.RecordAccepted("dock", scenario.Room, ScannerRoomScenarioFixture.CameraAId, scenario.PlayerA.SessionId, 0, "changed");

        string[] lines = (await RunAsync(CreateScannerRoomCommand(scenario, scenario.Diagnostics))).Split(Environment.NewLine);

        int header = Array.FindIndex(lines, line => line.StartsWith("Recent transitions"));
        lines[0].Should().Contain("events=41 (accepted=1, rejected=40, ").And.Contain("rejections without a log line=34");
        lines[header].Should().Be("Recent transitions (newest last, 2 of the 7 kept, repeats folded):");
        lines[header + 1].Should().Contain(" n=32 ").And.Contain(" ev=component out=reject ").And.EndWith(" rep=32 reason=non_owner (x6 since n=1)");
        lines[header + 2].Should().Contain(" n=41 ").And.EndWith(" reason=changed");
    }

    [TestMethod]
    public async Task ScannerMarkWritesAMarkerWithoutAnyScannerRoom()
    {
        CapturingLogger<ScannerRoomDiagnostics> scannerLog = new();
        CapturingLogger<BasePowerDiagnostics> powerLog = new();
        ScannerRoomDiagnostics diagnostics = new(scannerLog);
        ScannerRoomManager noRooms = ScannerRoomScenarioFixture.CreateManager(new EntityRegistry(NullLogger<EntityRegistry>.Instance), new SimulationOwnershipData(), diagnostics);
        ScannerMarkCommand command = new(noRooms, diagnostics, new BasePowerDiagnostics(powerLog));
        RecordingCommandContext context = new();

        await command.Execute(context, "T1 Start");

        scannerLog.Entries.Should().Contain(entry => entry.Message.StartsWith("[SRD1] n=- ") && entry.Message.Contains(" ev=mark out=info ") && entry.Message.EndsWith(" reason=t1_start"),
            "a test step must be markable before the first room is built");
        powerLog.Entries.Should().Contain(entry => entry.Message.StartsWith("[BPD1] n=- ") && entry.Message.EndsWith(" reason=t1_start"));
        context.Replies.Single().Should().Contain("reason=t1_start").And.Contain("no Scanner Rooms to checkpoint");
        diagnostics.GetCounters().Recorded.Should().Be(0);
    }

    [TestMethod]
    public async Task ScannerMarkCheckpointsEveryRoomAndRejectsAnEmptyLabel()
    {
        ScannerRoomScenarioFixture scenario = new();
        ScannerMarkCommand command = new(scenario.Manager, scenario.Diagnostics, new BasePowerDiagnostics());
        RecordingCommandContext context = new();

        await command.Execute(context, "!!!");
        await command.Execute(context, "D1-before-restart");

        context.Replies[0].Should().Contain("nothing was written");
        ScannerRoomDiagnosticEntry checkpoint = scenario.Diagnostics.GetHistory().Single();
        checkpoint.Format().Should().Contain(" ev=manual out=checkpoint ");
        checkpoint.Reason.Should().Be("d1-before-restart");
        context.Replies[1].Should().Contain($"1 Scanner Room checkpoint(s): {ScannerRoomScenarioFixture.RoomId}={checkpoint.StateFingerprint}");
    }

    [TestMethod]
    public async Task BasePowerCommandStatesTotalsAndAnomaliesBeforeTheListing()
    {
        ScannerRoomScenarioFixture scenario = new();
        ManualTimeProvider time = new(DateTimeOffset.UnixEpoch);
        BasePowerDiagnostics diagnostics = new(timeProvider: time);
        ModuleEntity healthy = AddSource(scenario, new PowerSourceMetadata(60f, 0f, BasePowerSourceType.SOLAR));
        ModuleEntity silent = AddSource(scenario, new PowerSourceMetadata(100f, 0f, BasePowerSourceType.THERMAL));
        ModuleEntity neverReported = AddSource(scenario, new PowerSourceMetadata(10f, 0f, BasePowerSourceType.SOLAR));
        ModuleEntity unowned = AddSource(scenario, new PowerSourceMetadata(5f, 0f, BasePowerSourceType.SOLAR));
        Assert.IsTrue(scenario.Ownership.TryAcquire(healthy.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));
        Assert.IsTrue(scenario.Ownership.TryAcquire(silent.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));
        Assert.IsTrue(scenario.Ownership.TryAcquire(neverReported.Id, scenario.PlayerB, SimulationLockType.TRANSIENT));
        scenario.PlayerB.GameMode = SubnauticaGameMode.CREATIVE;
        diagnostics.RecordAccepted(silent.Id, scenario.PlayerA.SessionId, (PowerSourceMetadata)silent.Metadata);
        time.Advance(TimeSpan.FromSeconds(95));
        diagnostics.RecordAccepted(healthy.Id, scenario.PlayerA.SessionId, (PowerSourceMetadata)healthy.Metadata);
        diagnostics.RecordConsumption(healthy.Id, scenario.PlayerB.SessionId, scenario.PlayerA.SessionId, 1.5f, ConsumptionRoute.FORWARD_TO_SIMULATOR,
            (PowerSourceMetadata)healthy.Metadata, "");
        diagnostics.RecordRejected(unowned.Id, scenario.PlayerB.SessionId, (PowerSourceMetadata)unowned.Metadata, "sender does not own source simulation");
        BasePowerCommand command = new(new BasePowerManager(scenario.EntityRegistry, scenario.Ownership, diagnostics, NullLogger<BasePowerManager>.Instance), diagnostics);

        string[] lines = (await RunAsync(command)).Split(Environment.NewLine);

        lines[0].Should().Be($"Base power ep={diagnostics.Epoch}: sources=4 (simulated=3, unowned=1), stored=175.00/475.00, updates accepted=2 rejected=1, consumption forwarded=1 (1.50) applied by server=0 (0.00) rejected=0, trace=off, diagnostics failures=0");
        lines[1].Should().Be("Anomalies (4):");
        string[] anomalies = lines.Skip(2).Take(4).ToArray();
        anomalies.Should().OnlyContain(line => line.StartsWith(" ! "));
        anomalies.Should().ContainSingle(line => line.Contains("THERMAL") && line.Contains("simulated by A #1, who last reported it 95s ago"));
        anomalies.Should().NotContain(line => line.Contains("CREATIVE"), "a source simulated by a Creative player takes what others draw from it like any other");
        anomalies.Should().ContainSingle(line => line.Contains("simulated by B #2, who has not reported it yet"));
        anomalies.Should().ContainSingle(line => line.Contains("has no simulating player"));
        anomalies.Should().ContainSingle(line => line.Contains("had 1 update(s) rejected, last reason: sender_does_not_own_source_simulation"));
        lines[6].Should().Be("Sources:");
        lines.Should().ContainSingle(line => line.StartsWith("- SOLAR ") && line.Contains(": 60.00/75.00, owner=A #1 (SURVIVAL), ") && line.Contains("owner=A #1 (SURVIVAL), last update 0.0s ago by #1, updates=1 rejected=0 handed back=0, consumed by others: #2 forwarded 1 (1.50) server-applied 0 (0.00) rejected 0, ") && line.Contains($"id={healthy.Id}"));
        lines.Should().ContainSingle(line => line.Contains("owner=<none>, no update since server start, updates=0 rejected=1 handed back=0, consumed by others: nothing reported"));
        lines.Should().ContainSingle(line => line.Contains("owner=B #2 (CREATIVE), "));
        lines[^1].Should().Be("Total source storage: 175.00/475.00");
    }

    [TestMethod]
    public async Task BasePowerCommandSwitchesTheTrace()
    {
        ScannerRoomScenarioFixture scenario = new();
        BasePowerDiagnostics diagnostics = new();
        BasePowerCommand command = new(new BasePowerManager(scenario.EntityRegistry, scenario.Ownership, diagnostics, NullLogger<BasePowerManager>.Instance), diagnostics);
        RecordingCommandContext context = new();

        await command.Execute(context, true);
        bool enabled = diagnostics.TraceEnabled;
        await command.Execute(context, false);

        enabled.Should().BeTrue();
        diagnostics.TraceEnabled.Should().BeFalse();
        context.Replies.Should().HaveCount(2);
        context.Replies[0].Should().StartWith("Base power live trace enabled");
        (await RunAsync(command)).Should().StartWith($"Base power ep={diagnostics.Epoch}: sources=0 ").And.Contain("Anomalies (0):");
    }

    [TestMethod]
    public void ServiceContainerCanBuildTheCommandsAndGatesThemForAdmins()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddWorld();
        services.AddCommands();
        // The commands read Scanner Rooms through their manager. What the manager needs beyond the world state
        // (network delivery, the player list, loading the world) is not part of this, so it is put together here.
        services.AddSingleton(provider => ScannerRoomScenarioFixture.CreateManager(provider.GetRequiredService<EntityRegistry>(),
            provider.GetRequiredService<SimulationOwnershipData>(), provider.GetRequiredService<ScannerRoomDiagnostics>()));
        using ServiceProvider provider = services.BuildServiceProvider();

        // The same entries the command registry is built from, without resolving every other command of the server.
        CommandHandlerEntry[] entries =
        [
            new(provider.GetRequiredService<ScannerRoomCommand>(), typeof(ICommandHandler)),
            new(provider.GetRequiredService<ScannerMarkCommand>(), typeof(ICommandHandler<string>)),
            new(provider.GetRequiredService<BasePowerCommand>(), typeof(ICommandHandler)),
            new(provider.GetRequiredService<BasePowerCommand>(), typeof(ICommandHandler<bool>))
        ];

        entries.Select(entry => entry.Name).Should().Equal("scannerroom", "scannermark", "basepower", "basepower");
        entries.Should().OnlyContain(entry => entry.MinimumPermissions == Perms.ADMIN, "these commands print and mark server state");
        entries.Should().OnlyContain(entry => entry.Description.Length > 0, "the help command lists them by this text");
    }

    private static ScannerRoomCommand CreateScannerRoomCommand(ScannerRoomScenarioFixture scenario, ScannerRoomDiagnostics diagnostics) =>
        new(scenario.EntityRegistry, diagnostics, scenario.Ownership, scenario.Manager);

    private static async Task<string> RunAsync(ICommandHandler command)
    {
        RecordingCommandContext context = new();
        await command.Execute(context);
        return context.Replies.Single();
    }

    private static ModuleEntity AddSource(ScannerRoomScenarioFixture scenario, PowerSourceMetadata metadata)
    {
        ModuleEntity source = ModuleEntity.MakeEmpty();
        source.Id = new NitroxId();
        source.Metadata = metadata;
        scenario.EntityRegistry.AddEntity(source);
        return source;
    }

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow() => now;

        public override long GetTimestamp() => now.UtcTicks;

        public void Advance(TimeSpan duration) => now += duration;
    }

    private sealed class RecordingCommandContext : ICommandContext
    {
        public List<string> Replies { get; } = [];
        public ILogger Logger { get; set; } = NullLogger.Instance;
        public CommandOrigin Origin { get; init; } = CommandOrigin.SERVER;
        public string OriginName => "TEST";
        public SessionId OriginId { get; init; } = SessionId.SERVER_ID;
        public Perms Permissions { get; init; } = Perms.HOST;

        public Task ReplyAsync<T>(T data)
        {
            Replies.Add(data?.ToString() ?? "");
            return Task.CompletedTask;
        }

        public ValueTask SendAsync<T>(SessionId sessionId, T data) => ValueTask.CompletedTask;

        public ValueTask SendToAllAsync<T>(T data) => ValueTask.CompletedTask;

        public ValueTask SendToOthersAsync<T>(T data) => ValueTask.CompletedTask;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception)));
    }
}
