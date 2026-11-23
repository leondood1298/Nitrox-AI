using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.Commands;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Test.Server.GameLogic;

namespace Nitrox.Test.Server.Commands;

/// <summary>
///     The <c>basepower</c> command is what a tester runs on the server console during a session, so its output has
///     to state totals and anomalies before the listing. With an argument it switches the live trace.
/// </summary>
[TestClass]
public sealed class BasePowerCommandTest
{
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

        string[] lines = (await RecordingCommandContext.RunAsync(command)).Split(Environment.NewLine);

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
        (await RecordingCommandContext.RunAsync(command)).Should().StartWith($"Base power ep={diagnostics.Epoch}: sources=0 ").And.Contain("Anomalies (0):");
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
}
