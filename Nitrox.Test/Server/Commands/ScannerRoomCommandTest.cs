using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Server.Subnautica.Models.Commands;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Test.Server.GameLogic;

namespace Nitrox.Test.Server.Commands;

/// <summary>
///     The <c>scannerroom</c> command is what a tester runs on the server console during a session, so its output has
///     to state totals and anomalies before the listings.
/// </summary>
[TestClass]
public sealed class ScannerRoomCommandTest
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

        string first = await RecordingCommandContext.RunAsync(command);
        string second = await RecordingCommandContext.RunAsync(command);

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

        string[] lines = (await RecordingCommandContext.RunAsync(CreateScannerRoomCommand(scenario, diagnostics))).Split(Environment.NewLine);

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
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB, ScannerRoomScenarioFixture.CameraAId, true)).Granted);
        ScannerRoomDiagnostics diagnostics = new(NullLogger<ScannerRoomDiagnostics>.Instance, scenario.Ownership);

        string output = await RecordingCommandContext.RunAsync(CreateScannerRoomCommand(scenario, diagnostics));

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

        string[] lines = (await RecordingCommandContext.RunAsync(CreateScannerRoomCommand(scenario, scenario.Diagnostics))).Split(Environment.NewLine);

        int header = Array.FindIndex(lines, line => line.StartsWith("Recent transitions"));
        lines[0].Should().Contain("events=41 (accepted=1, rejected=40, ").And.Contain("rejections without a log line=34");
        lines[header].Should().Be("Recent transitions (newest last, 2 of the 7 kept, repeats folded):");
        lines[header + 1].Should().Contain(" n=32 ").And.Contain(" ev=component out=reject ").And.EndWith(" rep=32 reason=non_owner (x6 since n=1)");
        lines[header + 2].Should().Contain(" n=41 ").And.EndWith(" reason=changed");
    }

    private static ScannerRoomCommand CreateScannerRoomCommand(ScannerRoomScenarioFixture scenario, ScannerRoomDiagnostics diagnostics) =>
        new(diagnostics, scenario.Ownership, scenario.Manager);
}
