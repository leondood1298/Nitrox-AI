using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
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

    private sealed class DiscardingPacketSender : IPacketSender
    {
        public ValueTask SendPacketAsync<T>(T packet, SessionId sessionId) where T : Packet => ValueTask.CompletedTask;

        public ValueTask SendPacketToAllAsync<T>(T packet) where T : Packet => ValueTask.CompletedTask;

        public ValueTask SendPacketToOthersAsync<T>(T packet, SessionId excludedSessionId) where T : Packet => ValueTask.CompletedTask;
    }
}
