using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Server.Subnautica.Models.Commands;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Test.Server.GameLogic;

namespace Nitrox.Test.Server.Commands;

/// <summary>
///     The <c>scannermark</c> command writes a label into the Scanner Room and base power logs, so that a tester
///     finds the lines of one test step in them.
/// </summary>
[TestClass]
public sealed class ScannerMarkCommandTest
{
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

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception)));
    }
}
