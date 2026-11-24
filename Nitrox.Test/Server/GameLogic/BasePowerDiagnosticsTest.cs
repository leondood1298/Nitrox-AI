using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Extensions;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Processors;
using Nitrox.Test.Server.Helper;

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
        List<(LogLevel Level, string Message)> processorLog = [];
        List<(LogLevel Level, string Message)> diagnosticsLog = [];
        BasePowerDiagnostics diagnostics = new(LogSubstitute.Capturing<BasePowerDiagnostics>(diagnosticsLog), scenario.Ownership);
        BasePowerSourceConsumeProcessor processor = new(scenario.EntityRegistry, CreateManager(scenario, diagnostics), diagnostics, LogSubstitute.Capturing<BasePowerSourceConsumeProcessor>(processorLog));

        // A consumer reports four times a second, each time a slightly different amount. Nobody simulates the
        // source and it has no stored power yet, so every report is rejected.
        for (int report = 0; report < 40; report++)
        {
            await processor.Process(scenario.Context(scenario.PlayerB),
                new BasePowerSourceConsume(source.Id, 0.25f + report * 0.001f));
        }

        (LogLevel Level, string Message)[] rejections = processorLog.Concat(diagnosticsLog).Where(entry => entry.Message.Contains("reject")).ToArray();
        rejections.Should().HaveCountLessThan(10, "ten seconds of a rejected consumer must not cost forty lines");
        rejections[0].Message.Should().Contain(" sid=2 ev=consume out=reject ").And.Contain(" owner=none ")
                .And.EndWith(" reason=source_has_no_stored_power_state_and_no_simulation_owner");
        diagnostics.RejectedUpdates.Should().Be(40, "thinning out the log must not thin out the count");
    }

    [TestMethod]
    public async Task RejectedUpdatesAreThinnedOutAndNameTheRealOwner()
    {
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity source = AddSolarPanel(scenario);
        source.Metadata = new PowerSourceMetadata(30f, 0f, BasePowerSourceType.SOLAR);
        Assert.IsTrue(scenario.Ownership.TryAcquire(source.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));
        List<(LogLevel Level, string Message)> processorLog = [];
        List<(LogLevel Level, string Message)> diagnosticsLog = [];
        BasePowerDiagnostics diagnostics = new(LogSubstitute.Capturing<BasePowerDiagnostics>(diagnosticsLog), scenario.Ownership);
        EntityMetadataUpdateProcessor processor = new(new PlayerManager(null!, null!, null!, NullLogger<PlayerManager>.Instance), scenario.EntityRegistry,
            scenario.Ownership, scenario.Manager, CreateManager(scenario, diagnostics), scenario.WorldEntityManager, scenario.EntitySimulation, scenario.ItemClaims,
            LogSubstitute.Capturing<EntityMetadataUpdateProcessor>(processorLog));

        // Player B wrongly believes it simulates the source and reports its level once a second.
        for (int report = 0; report < 40; report++)
        {
            await processor.Process(scenario.Context(scenario.PlayerB),
                new EntityMetadataUpdate(source.Id, new PowerSourceMetadata(10f + report, 0f, BasePowerSourceType.SOLAR)));
        }

        (LogLevel Level, string Message)[] rejections = processorLog.Concat(diagnosticsLog).Where(entry => entry.Message.Contains("reject")).ToArray();
        rejections.Should().HaveCountLessThan(10);
        rejections[0].Message.Should().Contain(" sid=2 ev=source_update out=reject ").And.Contain(" type=solar power=30.00/75.00 owner=1 ")
                .And.EndWith(" reason=sender_does_not_own_source_simulation");
        rejections[^1].Message.Should().Contain(" rep=32 ");
        rejections.Should().OnlyContain(entry => entry.Level == LogLevel.Information,
            "this is how the server answers a client that reports a source it does not simulate, not a fault of the server");
        diagnostics.RejectedUpdates.Should().Be(40);
        diagnostics.GetSources()[source.Id].RejectedUpdates.Should().Be(40);
    }

    [TestMethod]
    public async Task HandingASourceBackIsRecordedWithWhoHasItNow()
    {
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity source = AddSolarPanel(scenario);
        source.Metadata = new PowerSourceMetadata(60f, 0f, BasePowerSourceType.SOLAR);
        Assert.IsTrue(scenario.Ownership.TryAcquire(source.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));
        List<(LogLevel Level, string Message)> log = [];
        BasePowerDiagnostics diagnostics = new(LogSubstitute.Capturing<BasePowerDiagnostics>(log), scenario.Ownership);
        SimulationOwnershipReleaseProcessor processor = new(new PlayerManager(null!, null!, null!, NullLogger<PlayerManager>.Instance),
            scenario.EntitySimulation, scenario.EntityRegistry, diagnostics);

        // Player A does not have the source's object and gives its simulation back; nobody else is connected.
        await processor.Process(scenario.Context(scenario.PlayerA), new SimulationOwnershipRelease(source.Id));
        // Player B never held it.
        await processor.Process(scenario.Context(scenario.PlayerB), new SimulationOwnershipRelease(source.Id));

        string[] lines = log.Select(entry => entry.Message).Where(line => line.Contains(" ev=source_handback ")).ToArray();
        lines.Should().HaveCount(2);
        lines[0].Should().Contain(" sid=1 ev=source_handback out=ok ").And.Contain(" type=solar power=60.00/75.00 owner=none ")
                .And.EndWith(" reason=nobody_else_can_simulate");
        lines[1].Should().Contain(" sid=2 ev=source_handback out=reject ").And.EndWith(" reason=sender_holds_no_plain_simulation_lock");
        diagnostics.GetSources()[source.Id].HandBacks.Should().Be(1);
    }

    [TestMethod]
    public void HandBackOfSomethingThatIsNoPowerSourceIsNotBasePower()
    {
        ScannerRoomScenarioFixture scenario = new();
        List<(LogLevel Level, string Message)> log = [];
        BasePowerDiagnostics diagnostics = new(LogSubstitute.Capturing<BasePowerDiagnostics>(log), scenario.Ownership);

        diagnostics.RecordHandBack(scenario.Room, scenario.PlayerA.SessionId, true, true);
        diagnostics.RecordHandBack(AddSolarPanel(scenario), scenario.PlayerA.SessionId, true, true);

        log.Should().BeEmpty("the hand-back request is the same for every entity; only power sources belong in this log");
        diagnostics.GetSources().Should().BeEmpty();
    }

    [TestMethod]
    public void OnlyTheFirstUpdateOfAnOwnerGetsALine()
    {
        List<(LogLevel Level, string Message)> log = [];
        ManualTimeProvider time = new(new DateTimeOffset(2026, 11, 23, 14, 4, 5, TimeSpan.Zero));
        BasePowerDiagnostics diagnostics = new(LogSubstitute.Capturing<BasePowerDiagnostics>(log), timeProvider: time);

        for (int report = 1; report <= 100; report++)
        {
            diagnostics.RecordAccepted(sourceId, owner, new PowerSourceMetadata(report * 0.5f, 0f, BasePowerSourceType.SOLAR));
        }
        diagnostics.RecordAccepted(sourceId, consumer, new PowerSourceMetadata(50f, 0f, BasePowerSourceType.SOLAR));

        log.Select(entry => entry.Message).Should().Equal(
            $"[BPD1] n=- ep={diagnostics.Epoch} side=S sid=- ev=epoch out=info utc=2026-11-23T14:04:05Z reason=server_diagnostics_started",
            $"[BPD1] n=1 ep={diagnostics.Epoch} side=S sid=1 ev=source_update out=ok source=99e1d312.8c30 type=solar power=0.50/75.00 owner=? amount=- total=- reason=first_update",
            $"[BPD1] n=101 ep={diagnostics.Epoch} side=S sid=2 ev=source_update out=ok source=99e1d312.8c30 type=solar power=50.00/75.00 owner=? amount=- total=- reason=owner_changed_from_1");
        diagnostics.AcceptedUpdates.Should().Be(101);
    }

    [TestMethod]
    public void ConsumptionIsAddedUpPerConsumerAndRoute()
    {
        List<(LogLevel Level, string Message)> log = [];
        BasePowerDiagnostics diagnostics = new(LogSubstitute.Capturing<BasePowerDiagnostics>(log));
        PowerSourceMetadata stored = new(60f, 0f, BasePowerSourceType.SOLAR);
        SessionId secondConsumer = (SessionId)(ushort)3;

        for (int report = 0; report < 8; report++)
        {
            diagnostics.RecordConsumption(sourceId, consumer, owner, 0.5f, ConsumptionRoute.FORWARD_TO_SIMULATOR, stored, "");
        }
        diagnostics.RecordConsumption(sourceId, secondConsumer, owner, 0.5f, ConsumptionRoute.FORWARD_TO_SIMULATOR, stored, "");
        diagnostics.RecordConsumption(sourceId, consumer, null, 2f, ConsumptionRoute.APPLIED_BY_SERVER, new PowerSourceMetadata(58f, 0f, BasePowerSourceType.SOLAR), "");
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
        string[] forwardLines = log.Select(entry => entry.Message).Where(line => line.Contains(" sid=2 ev=consume out=forward ")).ToArray();
        forwardLines.Should().HaveCount(4, "the first two reports and then every power of two get a line");
        forwardLines[^1].Should().Contain(" amount=0.500 total=4.000 rep=8 reason=sent_to_owner_to_apply");
        log.Select(entry => entry.Message).Should().ContainSingle(line => line.Contains(" ev=consume out=server ") && line.Contains(" power=58.00/75.00 "));
    }

    [TestMethod]
    public void TimeSinceTheLastUpdateShowsAnOwnerThatWentSilent()
    {
        ManualTimeProvider time = new(DateTimeOffset.UnixEpoch);
        BasePowerDiagnostics diagnostics = new(timeProvider: time);
        diagnostics.RecordRejected(sourceId, consumer, new PowerSourceMetadata(40f, 0f, BasePowerSourceType.SOLAR), "sender does not own source simulation");
        diagnostics.GetSources()[sourceId].SinceLastUpdate.Should().BeNull("a rejected update says nothing about the owner being alive");

        diagnostics.RecordAccepted(sourceId, owner, new PowerSourceMetadata(40f, 0f, BasePowerSourceType.SOLAR));
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
            diagnostics.RecordRejected(new NitroxId(), consumer, new PowerSourceMetadata(0f, 0f, BasePowerSourceType.UNKNOWN), "unknown source entity");
            diagnostics.RecordConsumption(new NitroxId(), consumer, null, 1f, ConsumptionRoute.REJECTED, null, "unknown source");
        }

        diagnostics.GetSources().Should().BeEmpty("a client can make up as many ids as it likes");
        diagnostics.RejectedUpdates.Should().Be(BasePowerDiagnostics.SOURCE_CAPACITY * 4);
    }

    [TestMethod]
    public void FailingLoggerCannotBreakTheCodeBeingObserved()
    {
        BasePowerDiagnostics diagnostics = new(LogSubstitute.Throwing<BasePowerDiagnostics>());

        Action record = () =>
        {
            diagnostics.RecordAccepted(sourceId, owner, new PowerSourceMetadata(40f, 0f, BasePowerSourceType.SOLAR));
            diagnostics.RecordRejected(sourceId, consumer, new PowerSourceMetadata(40f, 0f, BasePowerSourceType.SOLAR), "sender does not own source simulation");
            diagnostics.RecordConsumption(sourceId, consumer, owner, 1f, ConsumptionRoute.FORWARD_TO_SIMULATOR, null, "");
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

    private static BasePowerManager CreateManager(ScannerRoomScenarioFixture scenario, BasePowerDiagnostics diagnostics) =>
        new(scenario.EntityRegistry, scenario.Ownership, diagnostics, NullLogger<BasePowerManager>.Instance);

    private static ModuleEntity AddSolarPanel(ScannerRoomScenarioFixture scenario)
    {
        ModuleEntity source = ModuleEntity.MakeEmpty();
        source.Id = new NitroxId();
        source.TechType = new NitroxTechType("SolarPanel");
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
}
