using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Models.Packets.Processors;

namespace Nitrox.Test.Server.Subnautica;

[TestClass]
public sealed class MapRoomCameraComponentStateValidationTest
{
    [TestMethod]
    [DataRow(0f, 0f, true)]
    [DataRow(100f, 400f, true)]
    [DataRow(-0.01f, 400f, false)]
    [DataRow(100.01f, 400f, false)]
    [DataRow(100f, -0.01f, false)]
    [DataRow(100f, 400.01f, false)]
    public void RequiresCanonicalComponentRange(float energy, float health, bool expected)
    {
        Assert.AreEqual(expected, ScannerRoomManager.IsValidComponentState(energy, health));
    }

    [TestMethod]
    public void RejectsNonFiniteValues()
    {
        Assert.IsFalse(ScannerRoomManager.IsValidComponentState(float.NaN, 400f));
        Assert.IsFalse(ScannerRoomManager.IsValidComponentState(float.PositiveInfinity, 400f));
        Assert.IsFalse(ScannerRoomManager.IsValidComponentState(float.NegativeInfinity, 400f));
        Assert.IsFalse(ScannerRoomManager.IsValidComponentState(100f, float.NaN));
        Assert.IsFalse(ScannerRoomManager.IsValidComponentState(100f, float.PositiveInfinity));
        Assert.IsFalse(ScannerRoomManager.IsValidComponentState(100f, float.NegativeInfinity));
    }

    [TestMethod]
    [DataRow(100f, 100)]
    [DataRow(75.01f, 100)]
    [DataRow(75f, 75)]
    [DataRow(50f, 50)]
    [DataRow(25f, 25)]
    [DataRow(10f, 10)]
    [DataRow(0f, 0)]
    public void EnergyBandsOnlyLogMeaningfulThresholdCrossings(float value, int expected)
    {
        Assert.AreEqual(expected, ScannerRoomManager.GetEnergyBand(value));
    }

    [TestMethod]
    [DataRow(400f, 100)]
    [DataRow(300.01f, 100)]
    [DataRow(300f, 75)]
    [DataRow(200f, 50)]
    [DataRow(100f, 25)]
    [DataRow(40f, 10)]
    [DataRow(0f, 0)]
    public void HealthBandsAreNormalizedToCameraMaximum(float value, int expected)
    {
        Assert.AreEqual(expected, ScannerRoomManager.GetHealthBand(value));
    }

    [TestMethod]
    public void NewCameraStateDefaultsToFullCanonicalValues()
    {
        NitroxId cameraId = new();
        MapRoomCameraRecord record = new(cameraId, 1);
        MapRoomCameraDock dockPacket = new(cameraId, new NitroxId(), 0);

        Assert.AreEqual(MapRoomCameraRecord.MAX_ENERGY, record.Energy);
        Assert.AreEqual(MapRoomCameraRecord.MAX_HEALTH, record.Health);
        Assert.AreEqual(MapRoomCameraRecord.MAX_ENERGY, dockPacket.Energy);
        Assert.AreEqual(MapRoomCameraRecord.MAX_HEALTH, dockPacket.Health);
    }

    [TestMethod]
    public async Task ExclusiveCameraOwnerWinsConcurrentWriteAgainstDockedRoomOwner()
    {
        ScannerRoomScenarioFixture scenario = await CreateDockedScenarioAsync();
        Assert.IsTrue(scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.RoomId, scenario.PlayerA, SimulationLockType.TRANSIENT));
        Assert.IsTrue(scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.CameraAId, scenario.PlayerB, SimulationLockType.EXCLUSIVE));
        MapRoomCameraComponentStateProcessor processor = new(scenario.Manager);

        using Barrier start = new(2);
        Task roomWrite = Task.Run(async () =>
        {
            start.SignalAndWait();
            await processor.Process(
                scenario.Context(scenario.PlayerA),
                new MapRoomCameraComponentState(ScannerRoomScenarioFixture.CameraAId, 80f, 90f));
        });
        Task cameraWrite = Task.Run(async () =>
        {
            start.SignalAndWait();
            await processor.Process(
                scenario.Context(scenario.PlayerB),
                new MapRoomCameraComponentState(ScannerRoomScenarioFixture.CameraAId, 25f, 50f));
        });
        await Task.WhenAll(roomWrite, cameraWrite);

        // The room's simulating player is answered alone, the camera's controller through the one broadcast.
        MapRoomCameraComponentState rejected = scenario.SentTo<MapRoomCameraComponentState>(scenario.PlayerA).Single();
        Assert.IsFalse(rejected.Granted);

        MapRoomCameraComponentState accepted = scenario.SentToAll<MapRoomCameraComponentState>().Single();
        Assert.IsTrue(accepted.Granted);
        Assert.AreEqual(25f, accepted.Energy);
        Assert.AreEqual(50f, accepted.Health);
        Assert.AreEqual(1, accepted.Revision);
        Assert.AreEqual(0, scenario.SentTo<MapRoomCameraComponentState>(scenario.PlayerB).Count);

        MapRoomCameraRecord record = scenario.Room.GetCameraRecord(ScannerRoomScenarioFixture.CameraAId)!;
        Assert.AreEqual(25f, record.Energy);
        Assert.AreEqual(50f, record.Health);
        Assert.AreEqual(1, record.ComponentRevision);
    }

    [TestMethod]
    public async Task TransientCameraLockDoesNotBlockDockedRoomOwner()
    {
        ScannerRoomScenarioFixture scenario = await CreateDockedScenarioAsync();
        Assert.IsTrue(scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.RoomId, scenario.PlayerA, SimulationLockType.TRANSIENT));
        Assert.IsTrue(scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.CameraAId, scenario.PlayerB, SimulationLockType.TRANSIENT));
        MapRoomCameraComponentStateProcessor processor = new(scenario.Manager);

        await processor.Process(
            scenario.Context(scenario.PlayerA),
            new MapRoomCameraComponentState(ScannerRoomScenarioFixture.CameraAId, 90f, 80f));

        MapRoomCameraComponentState response = scenario.Sent<MapRoomCameraComponentState>().Single();
        Assert.IsTrue(response.Granted);
        Assert.AreEqual(1, response.Revision);
        MapRoomCameraRecord record = scenario.Room.GetCameraRecord(ScannerRoomScenarioFixture.CameraAId)!;
        Assert.AreEqual(90f, record.Energy);
        Assert.AreEqual(80f, record.Health);
        Assert.AreEqual(1, record.ComponentRevision);
    }

    [TestMethod]
    public async Task ExactDuplicateIsGrantedWithoutRevisionBump()
    {
        ScannerRoomScenarioFixture scenario = await CreateDockedScenarioAsync();
        Assert.IsTrue(scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.RoomId, scenario.PlayerA, SimulationLockType.TRANSIENT));
        MapRoomCameraComponentStateProcessor processor = new(scenario.Manager);
        AuthProcessorContext context = scenario.Context(scenario.PlayerA);

        await processor.Process(context, new MapRoomCameraComponentState(ScannerRoomScenarioFixture.CameraAId, 75f, 50f));
        await processor.Process(context, new MapRoomCameraComponentState(ScannerRoomScenarioFixture.CameraAId, 75f, 50f));

        MapRoomCameraComponentState[] responses = scenario.Sent<MapRoomCameraComponentState>().ToArray();
        Assert.AreEqual(2, responses.Length);
        Assert.IsTrue(responses.All(response => response.Granted));
        Assert.IsTrue(responses.All(response => response.Revision == 1));
        Assert.IsTrue(responses.All(response => response.Energy == 75f && response.Health == 50f));
        MapRoomCameraRecord record = scenario.Room.GetCameraRecord(ScannerRoomScenarioFixture.CameraAId)!;
        Assert.AreEqual(1, record.ComponentRevision);
    }

    [TestMethod]
    public async Task ConcurrentAcceptedResponsesUseAtomicValuesAndRevisions()
    {
        using BlockingLogger logger = new();
        ScannerRoomScenarioFixture scenario = await CreateDockedScenarioAsync(logger);
        Assert.IsTrue(scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.RoomId, scenario.PlayerA, SimulationLockType.TRANSIENT));
        MapRoomCameraComponentStateProcessor processor = new(scenario.Manager);
        MapRoomCameraRecord record = scenario.Room.GetCameraRecord(ScannerRoomScenarioFixture.CameraAId)!;
        logger.BlockNextEntry();

        Task firstWrite = Task.Run(() => processor.Process(
            scenario.Context(scenario.PlayerA),
            new MapRoomCameraComponentState(ScannerRoomScenarioFixture.CameraAId, 10f, 10f)));

        bool firstReachedDiagnostics = logger.BlockedEntry.Wait(TimeSpan.FromSeconds(5));
        Task? secondWrite = null;
        bool secondWaited = false;
        if (firstReachedDiagnostics)
        {
            secondWrite = Task.Run(() => processor.Process(
                scenario.Context(scenario.PlayerA),
                new MapRoomCameraComponentState(ScannerRoomScenarioFixture.CameraAId, 50f, 50f)));
            // The first write is paused before it has left the Scanner Room manager, which handles one request at a
            // time, so the second one cannot have changed the camera yet.
            secondWaited = !SpinWait.SpinUntil(() => record.ComponentRevision == 2, TimeSpan.FromMilliseconds(250));
        }
        logger.ReleaseBlockedEntry.Set();
        await firstWrite;
        if (secondWrite is not null)
        {
            await secondWrite;
        }

        Assert.IsTrue(firstReachedDiagnostics, "The first accepted transition did not reach diagnostics.");
        Assert.IsTrue(secondWaited, "The second writer changed the camera while the first accepted change was still being handled.");
        MapRoomCameraComponentState[] responses = scenario.SentToAll<MapRoomCameraComponentState>().ToArray();
        Assert.AreEqual(2, responses.Length);
        MapRoomCameraComponentState firstResponse = responses[0];
        MapRoomCameraComponentState secondResponse = responses[1];
        Assert.AreEqual(10f, firstResponse.Energy);
        Assert.AreEqual(10f, firstResponse.Health);
        Assert.AreEqual(1, firstResponse.Revision);
        Assert.AreEqual(50f, secondResponse.Energy);
        Assert.AreEqual(50f, secondResponse.Health);
        Assert.AreEqual(2, secondResponse.Revision);
        Assert.AreEqual(secondResponse.Energy, record.Energy);
        Assert.AreEqual(secondResponse.Health, record.Health);
        Assert.AreEqual(secondResponse.Revision, record.ComponentRevision);
    }

    [TestMethod]
    public async Task ProcessRejectsOutOfRangeValuesWithoutMutatingCanonicalState()
    {
        ScannerRoomScenarioFixture scenario = await CreateDockedScenarioAsync();
        Assert.IsTrue(scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.RoomId, scenario.PlayerA, SimulationLockType.TRANSIENT));
        MapRoomCameraComponentStateProcessor processor = new(scenario.Manager);

        await processor.Process(
            scenario.Context(scenario.PlayerA),
            new MapRoomCameraComponentState(ScannerRoomScenarioFixture.CameraAId, 100f, 400.01f));

        Assert.IsFalse(scenario.Sent<MapRoomCameraComponentState>().Single().Granted);
        MapRoomCameraRecord record = scenario.Room.GetCameraRecord(ScannerRoomScenarioFixture.CameraAId)!;
        Assert.AreEqual(MapRoomCameraRecord.MAX_ENERGY, record.Energy);
        Assert.AreEqual(MapRoomCameraRecord.MAX_HEALTH, record.Health);
        Assert.AreEqual(0, record.ComponentRevision);
    }

    private static async Task<ScannerRoomScenarioFixture> CreateDockedScenarioAsync(ILogger<ScannerRoomDiagnostics>? diagnosticsLog = null)
    {
        ScannerRoomScenarioFixture scenario = new(diagnosticsLog: diagnosticsLog);
        MapRoomCameraDock dock = await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true);
        Assert.IsTrue(dock.Granted);
        return scenario;
    }

    /// <summary>
    ///     Holds up one log entry, when asked to, until the test lets it go.
    /// </summary>
    private sealed class BlockingLogger : ILogger<ScannerRoomDiagnostics>, IDisposable
    {
        private int blockNext;

        internal ManualResetEventSlim BlockedEntry { get; } = new();
        internal ManualResetEventSlim ReleaseBlockedEntry { get; } = new();

        internal void BlockNextEntry() => Volatile.Write(ref blockNext, 1);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (Interlocked.Exchange(ref blockNext, 0) == 1)
            {
                BlockedEntry.Set();
                ReleaseBlockedEntry.Wait(TimeSpan.FromSeconds(5));
            }
        }

        public void Dispose()
        {
            BlockedEntry.Dispose();
            ReleaseBlockedEntry.Dispose();
        }
    }
}
