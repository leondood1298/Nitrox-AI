using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Extensions;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Models.Packets.Processors;
using Nitrox.Test.Server.Subnautica;
using Packet = Nitrox.Model.Packets.Packet;

namespace Nitrox.Test.Server.GameLogic;

[TestClass]
public sealed class BasePowerDiagnosticsTest
{
    private static readonly NitroxId sourceId = new("99e1d312-b017-474e-8c30-ffc66573465f");
    private static readonly SessionId owner = (SessionId)(ushort)1;
    private static readonly SessionId consumer = (SessionId)(ushort)2;

    [TestMethod]
    public async Task RejectedConsumptionDoesNotWriteAWarningPerPacket()
    {
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity source = AddSolarPanel(scenario);
        CapturingLogger<BasePowerSourceConsumeProcessor> processorLog = new();
        CapturingLogger<BasePowerDiagnostics> diagnosticsLog = new();
        BasePowerDiagnostics diagnostics = new(diagnosticsLog, scenario.Ownership);
        BasePowerSourceConsumeProcessor processor = new(scenario.EntityRegistry, scenario.Ownership, new BasePowerSourceAuthority(), diagnostics, processorLog);

        // A consumer reports four times a second, each time a slightly different amount. Nobody simulates the
        // source and it has no stored power yet, so every report is rejected.
        for (int report = 0; report < 40; report++)
        {
            await processor.Process(new AuthProcessorContext(scenario.PlayerB, new DiscardingPacketSender()),
                new BasePowerSourceConsume(source.Id, 0.25f + report * 0.001f));
        }

        (LogLevel Level, string Message)[] warnings = processorLog.Entries.Concat(diagnosticsLog.Entries).Where(entry => entry.Level == LogLevel.Warning).ToArray();
        warnings.Should().HaveCountLessThan(10, "ten seconds of a rejected consumer must not cost forty lines");
        warnings[0].Message.Should().Contain(" sid=2 ev=consume out=reject ").And.Contain(" owner=none ")
                .And.EndWith(" reason=source_has_no_stored_power_state_and_no_simulation_owner");
        diagnostics.RejectedUpdates.Should().Be(40, "thinning out the log must not thin out the count");
    }

    [TestMethod]
    public async Task RejectedUpdatesAreThinnedOutAndNameTheRealOwner()
    {
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity source = AddSolarPanel(scenario);
        source.Metadata = new PowerSourceMetadata(30f, 75f, BasePowerSourceType.SOLAR, 5);
        Assert.IsTrue(scenario.Ownership.TryAcquire(source.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));
        CapturingLogger<BasePowerSourceUpdateProcessor> processorLog = new();
        CapturingLogger<BasePowerDiagnostics> diagnosticsLog = new();
        BasePowerDiagnostics diagnostics = new(diagnosticsLog, scenario.Ownership);
        BasePowerSourceUpdateProcessor processor = new(scenario.EntityRegistry, scenario.Ownership, new BasePowerSourceAuthority(), diagnostics, processorLog);

        // Player B wrongly believes it simulates the source and reports its level once a second.
        for (int report = 0; report < 40; report++)
        {
            await processor.Process(new AuthProcessorContext(scenario.PlayerB, new DiscardingPacketSender()),
                new BasePowerSourceUpdate(source.Id, BasePowerSourceType.SOLAR, 10f + report, report + 1));
        }

        (LogLevel Level, string Message)[] warnings = processorLog.Entries.Concat(diagnosticsLog.Entries).Where(entry => entry.Level == LogLevel.Warning).ToArray();
        warnings.Should().HaveCountLessThan(10);
        warnings[0].Message.Should().Contain(" sid=2 ev=source_update out=reject ").And.Contain(" type=solar power=30.00/75.00 rev=5 owner=1 ")
                .And.EndWith(" reason=sender_does_not_own_source_simulation");
        warnings[^1].Message.Should().Contain(" rep=32 ");
        diagnostics.RejectedUpdates.Should().Be(40);
        diagnostics.GetSources()[source.Id].RejectedUpdates.Should().Be(40);
    }

    [TestMethod]
    public void OnlyTheFirstUpdateOfAnOwnerGetsALine()
    {
        CapturingLogger<BasePowerDiagnostics> log = new();
        ManualTimeProvider time = new(new DateTimeOffset(2026, 11, 23, 14, 4, 5, TimeSpan.Zero));
        BasePowerDiagnostics diagnostics = new(log, timeProvider: time);

        for (int revision = 1; revision <= 100; revision++)
        {
            diagnostics.RecordAccepted(sourceId, owner, new PowerSourceMetadata(revision, 75f, BasePowerSourceType.SOLAR, revision));
        }
        diagnostics.RecordAccepted(sourceId, consumer, new PowerSourceMetadata(50f, 75f, BasePowerSourceType.SOLAR, 101));

        log.Entries.Select(entry => entry.Message).Should().Equal(
            $"[BPD1] n=- ep={diagnostics.Epoch} side=S sid=- ev=epoch out=info utc=2026-11-23T14:04:05Z reason=server_diagnostics_started",
            $"[BPD1] n=1 ep={diagnostics.Epoch} side=S sid=1 ev=source_update out=ok source=99e1d312.8c30 type=solar power=1.00/75.00 rev=1 owner=? amount=- total=- reason=first_update",
            $"[BPD1] n=101 ep={diagnostics.Epoch} side=S sid=2 ev=source_update out=ok source=99e1d312.8c30 type=solar power=50.00/75.00 rev=101 owner=? amount=- total=- reason=owner_changed_from_1");
        diagnostics.AcceptedUpdates.Should().Be(101);
    }

    [TestMethod]
    public void RevisionThatDoesNotAdvanceIsReported()
    {
        CapturingLogger<BasePowerDiagnostics> log = new();
        BasePowerDiagnostics diagnostics = new(log);
        diagnostics.RecordAccepted(sourceId, owner, new PowerSourceMetadata(40f, 75f, BasePowerSourceType.SOLAR, 57));

        // The source was rebuilt: the server starts counting at one again while clients still hold revision 57.
        diagnostics.RecordAccepted(sourceId, owner, new PowerSourceMetadata(10f, 75f, BasePowerSourceType.SOLAR, 1));

        log.Entries[^1].Level.Should().Be(LogLevel.Warning);
        log.Entries[^1].Message.Should().Contain(" ev=source_update out=invariant ").And.EndWith(" reason=revision_1_after_57");
        diagnostics.GetSources()[sourceId].RevisionRegressions.Should().Be(1);
        diagnostics.GetTotals().RevisionRegressions.Should().Be(1);
    }

    [TestMethod]
    public void ConsumptionIsAddedUpPerConsumerAndRoute()
    {
        CapturingLogger<BasePowerDiagnostics> log = new();
        BasePowerDiagnostics diagnostics = new(log);
        PowerSourceMetadata stored = new(60f, 75f, BasePowerSourceType.SOLAR, 5);
        SessionId secondConsumer = (SessionId)(ushort)3;

        for (int report = 0; report < 8; report++)
        {
            diagnostics.RecordConsumption(sourceId, consumer, owner, 0.5f, ConsumptionRoute.FORWARD_TO_OWNER, stored, "");
        }
        diagnostics.RecordConsumption(sourceId, secondConsumer, owner, 0.5f, ConsumptionRoute.FORWARD_TO_OWNER, stored, "");
        diagnostics.RecordConsumption(sourceId, consumer, null, 2f, ConsumptionRoute.APPLIED_BY_SERVER, new PowerSourceMetadata(58f, 75f, BasePowerSourceType.SOLAR, 6), "");
        diagnostics.RecordConsumption(sourceId, consumer, owner, float.NaN, ConsumptionRoute.REJECTED, stored, "amount NaN is outside 0-2500");

        BasePowerSourceDiagnostics source = diagnostics.GetSources()[sourceId];
        source.Consumers.Should().Equal(
            new BasePowerConsumerDiagnostics(consumer, 8, 4f, 1, 2f, 1),
            new BasePowerConsumerDiagnostics(secondConsumer, 1, 0.5f, 0, 0f, 0));
        BasePowerDiagnosticTotals totals = diagnostics.GetTotals();
        totals.ConsumptionForwarded.Should().Be(9);
        totals.ConsumptionForwardedAmount.Should().Be(4.5f);
        totals.ConsumptionAppliedByServer.Should().Be(1);
        totals.ConsumptionRejected.Should().Be(1);
        string[] forwardLines = log.Entries.Select(entry => entry.Message).Where(line => line.Contains(" sid=2 ev=consume out=forward ")).ToArray();
        forwardLines.Should().HaveCount(4, "the first two reports and then every power of two get a line");
        forwardLines[^1].Should().Contain(" amount=0.500 total=4.000 rep=8 reason=sent_to_owner_to_apply");
        log.Entries.Select(entry => entry.Message).Should().ContainSingle(line => line.Contains(" ev=consume out=server ") && line.Contains(" power=58.00/75.00 rev=6 "));
    }

    [TestMethod]
    public void TimeSinceTheLastUpdateShowsAnOwnerThatWentSilent()
    {
        ManualTimeProvider time = new(DateTimeOffset.UnixEpoch);
        BasePowerDiagnostics diagnostics = new(timeProvider: time);
        diagnostics.RecordRejected(sourceId, consumer, new PowerSourceMetadata(40f, 75f, BasePowerSourceType.SOLAR, 3), "sender does not own source simulation");
        diagnostics.GetSources()[sourceId].SinceLastUpdate.Should().BeNull("a rejected update says nothing about the owner being alive");

        diagnostics.RecordAccepted(sourceId, owner, new PowerSourceMetadata(40f, 75f, BasePowerSourceType.SOLAR, 4));
        time.Advance(TimeSpan.FromSeconds(95));

        BasePowerSourceDiagnostics source = diagnostics.GetSources()[sourceId];
        source.LastOwner.Should().Be(owner);
        source.SinceLastUpdate.Should().Be(TimeSpan.FromSeconds(95));
    }

    [TestMethod]
    public void IdsTheServerHasNoSourceForAreNotKept()
    {
        BasePowerDiagnostics diagnostics = new();

        for (int index = 0; index < BasePowerDiagnostics.SOURCE_CAPACITY * 2; index++)
        {
            diagnostics.RecordRejected(new NitroxId(), consumer, new PowerSourceMetadata(0f), "unknown source entity");
            diagnostics.RecordConsumption(new NitroxId(), consumer, null, 1f, ConsumptionRoute.REJECTED, null, "unknown source");
        }

        diagnostics.GetSources().Should().BeEmpty("a client can make up as many ids as it likes");
        diagnostics.RejectedUpdates.Should().Be(BasePowerDiagnostics.SOURCE_CAPACITY * 4);
    }

    [TestMethod]
    public void FailingLoggerCannotBreakTheCodeBeingObserved()
    {
        BasePowerDiagnostics diagnostics = new(new ThrowingLogger());

        Action record = () =>
        {
            diagnostics.RecordAccepted(sourceId, owner, new PowerSourceMetadata(40f, 75f, BasePowerSourceType.SOLAR, 1));
            diagnostics.RecordRejected(sourceId, consumer, new PowerSourceMetadata(40f, 75f, BasePowerSourceType.SOLAR, 1), "sender does not own source simulation");
            diagnostics.RecordConsumption(sourceId, consumer, owner, 1f, ConsumptionRoute.FORWARD_TO_OWNER, null, "");
            diagnostics.RecordMark("step-1");
        };

        record.Should().NotThrow();
        diagnostics.AcceptedUpdates.Should().Be(1);
        diagnostics.RejectedUpdates.Should().Be(1);
        diagnostics.OwnFailures.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void ServiceContainerGivesDiagnosticsTheLockTable()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddWorld();
        using ServiceProvider provider = services.BuildServiceProvider();

        BasePowerDiagnostics diagnostics = provider.GetRequiredService<BasePowerDiagnostics>();

        diagnostics.DescribeOwner(new NitroxId()).Should().Be(BasePowerDiagnostics.NO_OWNER);
    }

    private static ModuleEntity AddSolarPanel(ScannerRoomScenarioFixture scenario)
    {
        ModuleEntity source = ModuleEntity.MakeEmpty();
        source.Id = new NitroxId();
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

    private sealed class DiscardingPacketSender : IPacketSender
    {
        public ValueTask SendPacketAsync<T>(T packet, SessionId sessionId) where T : Packet => ValueTask.CompletedTask;

        public ValueTask SendPacketToAllAsync<T>(T packet) where T : Packet => ValueTask.CompletedTask;

        public ValueTask SendPacketToOthersAsync<T>(T packet, SessionId excludedSessionId) where T : Packet => ValueTask.CompletedTask;
    }

    private sealed class ThrowingLogger : ILogger<BasePowerDiagnostics>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => throw new InvalidOperationException("log sink is gone");
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
