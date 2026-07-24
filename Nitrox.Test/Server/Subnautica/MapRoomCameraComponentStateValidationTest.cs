using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Models.Packets.Processors;

namespace Nitrox.Test.Server.Subnautica;

[TestClass]
public sealed class MapRoomCameraComponentStateValidationTest
{
    [DataTestMethod]
    [DataRow(0f, 0f, true)]
    [DataRow(100f, 400f, true)]
    [DataRow(-0.01f, 400f, false)]
    [DataRow(100.01f, 400f, false)]
    [DataRow(100f, -0.01f, false)]
    [DataRow(100f, 400.01f, false)]
    public void RequiresCanonicalComponentRange(float energy, float health, bool expected)
    {
        Assert.AreEqual(expected, MapRoomCameraComponentStateProcessor.IsValidComponentState(energy, health));
    }

    [TestMethod]
    public void RejectsNonFiniteValues()
    {
        Assert.IsFalse(MapRoomCameraComponentStateProcessor.IsValidComponentState(float.NaN, 400f));
        Assert.IsFalse(MapRoomCameraComponentStateProcessor.IsValidComponentState(float.PositiveInfinity, 400f));
        Assert.IsFalse(MapRoomCameraComponentStateProcessor.IsValidComponentState(float.NegativeInfinity, 400f));
        Assert.IsFalse(MapRoomCameraComponentStateProcessor.IsValidComponentState(100f, float.NaN));
        Assert.IsFalse(MapRoomCameraComponentStateProcessor.IsValidComponentState(100f, float.PositiveInfinity));
        Assert.IsFalse(MapRoomCameraComponentStateProcessor.IsValidComponentState(100f, float.NegativeInfinity));
    }

    [DataTestMethod]
    [DataRow(100f, 100)]
    [DataRow(75.01f, 100)]
    [DataRow(75f, 75)]
    [DataRow(50f, 50)]
    [DataRow(25f, 25)]
    [DataRow(10f, 10)]
    [DataRow(0f, 0)]
    public void EnergyBandsOnlyLogMeaningfulThresholdCrossings(float value, int expected)
    {
        Assert.AreEqual(expected, MapRoomCameraComponentStateProcessor.GetEnergyBand(value));
    }

    [DataTestMethod]
    [DataRow(400f, 100)]
    [DataRow(300.01f, 100)]
    [DataRow(300f, 75)]
    [DataRow(200f, 50)]
    [DataRow(100f, 25)]
    [DataRow(40f, 10)]
    [DataRow(0f, 0)]
    public void HealthBandsAreNormalizedToCameraMaximum(float value, int expected)
    {
        Assert.AreEqual(expected, MapRoomCameraComponentStateProcessor.GetHealthBand(value));
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
        Assert.IsTrue(scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.RoomId, scenario.PlayerA, SimulationLockType.TRANSIENT));
        Assert.IsTrue(scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.CameraAId, scenario.PlayerB, SimulationLockType.EXCLUSIVE));
        MapRoomCameraComponentStateProcessor processor = new(scenario.Ownership, scenario.EntityRegistry, scenario.Diagnostics);
        RecordingPacketSender roomSender = new();
        RecordingPacketSender cameraSender = new();

        using Barrier start = new(2);
        Task roomWrite = Task.Run(async () =>
        {
            start.SignalAndWait();
            await processor.Process(
                new AuthProcessorContext(scenario.PlayerA, roomSender),
                new MapRoomCameraComponentState(ScannerRoomScenarioFixture.CameraAId, 80f, 90f));
        });
        Task cameraWrite = Task.Run(async () =>
        {
            start.SignalAndWait();
            await processor.Process(
                new AuthProcessorContext(scenario.PlayerB, cameraSender),
                new MapRoomCameraComponentState(ScannerRoomScenarioFixture.CameraAId, 25f, 50f));
        });
        await Task.WhenAll(roomWrite, cameraWrite);

        MapRoomCameraComponentState rejected = roomSender.Single<MapRoomCameraComponentState>();
        Assert.IsFalse(rejected.Granted);
        Assert.AreEqual(1, roomSender.DirectPackets);
        Assert.AreEqual(0, roomSender.BroadcastPackets);

        MapRoomCameraComponentState accepted = cameraSender.Single<MapRoomCameraComponentState>();
        Assert.IsTrue(accepted.Granted);
        Assert.AreEqual(25f, accepted.Energy);
        Assert.AreEqual(50f, accepted.Health);
        Assert.AreEqual(1, accepted.Revision);
        Assert.AreEqual(0, cameraSender.DirectPackets);
        Assert.AreEqual(1, cameraSender.BroadcastPackets);

        MapRoomCameraRecord record = scenario.Room.GetCameraRecord(ScannerRoomScenarioFixture.CameraAId)!;
        Assert.AreEqual(25f, record.Energy);
        Assert.AreEqual(50f, record.Health);
        Assert.AreEqual(1, record.ComponentRevision);
    }

    [TestMethod]
    public async Task TransientCameraLockDoesNotBlockDockedRoomOwner()
    {
        ScannerRoomScenarioFixture scenario = await CreateDockedScenarioAsync();
        Assert.IsTrue(scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.RoomId, scenario.PlayerA, SimulationLockType.TRANSIENT));
        Assert.IsTrue(scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.CameraAId, scenario.PlayerB, SimulationLockType.TRANSIENT));
        MapRoomCameraComponentStateProcessor processor = new(scenario.Ownership, scenario.EntityRegistry, scenario.Diagnostics);
        RecordingPacketSender sender = new();

        await processor.Process(
            new AuthProcessorContext(scenario.PlayerA, sender),
            new MapRoomCameraComponentState(ScannerRoomScenarioFixture.CameraAId, 90f, 80f));

        MapRoomCameraComponentState response = sender.Single<MapRoomCameraComponentState>();
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
        Assert.IsTrue(scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.RoomId, scenario.PlayerA, SimulationLockType.TRANSIENT));
        MapRoomCameraComponentStateProcessor processor = new(scenario.Ownership, scenario.EntityRegistry, scenario.Diagnostics);
        RecordingPacketSender sender = new();
        AuthProcessorContext context = new(scenario.PlayerA, sender);

        await processor.Process(context, new MapRoomCameraComponentState(ScannerRoomScenarioFixture.CameraAId, 75f, 50f));
        await processor.Process(context, new MapRoomCameraComponentState(ScannerRoomScenarioFixture.CameraAId, 75f, 50f));

        MapRoomCameraComponentState[] responses = sender.All<MapRoomCameraComponentState>();
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
        ScannerRoomScenarioFixture scenario = await CreateDockedScenarioAsync();
        Assert.IsTrue(scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.RoomId, scenario.PlayerA, SimulationLockType.TRANSIENT));
        using BlockingLogger logger = new();
        ScannerRoomDiagnostics diagnostics = new(logger);
        MapRoomCameraComponentStateProcessor processor = new(scenario.Ownership, scenario.EntityRegistry, diagnostics);
        RecordingPacketSender firstSender = new();
        RecordingPacketSender secondSender = new();
        MapRoomCameraRecord record = scenario.Room.GetCameraRecord(ScannerRoomScenarioFixture.CameraAId)!;

        Task firstWrite = Task.Run(() => processor.Process(
            new AuthProcessorContext(scenario.PlayerA, firstSender),
            new MapRoomCameraComponentState(ScannerRoomScenarioFixture.CameraAId, 10f, 10f)));

        bool firstReachedDiagnostics = logger.FirstEntry.Wait(TimeSpan.FromSeconds(5));
        Task? secondWrite = null;
        bool secondMutationCompleted = false;
        if (firstReachedDiagnostics)
        {
            secondWrite = Task.Run(() => processor.Process(
                new AuthProcessorContext(scenario.PlayerA, secondSender),
                new MapRoomCameraComponentState(ScannerRoomScenarioFixture.CameraAId, 50f, 50f)));
            secondMutationCompleted = SpinWait.SpinUntil(() =>
            {
                lock (record)
                {
                    return record.ComponentRevision == 2;
                }
            }, TimeSpan.FromSeconds(5));
        }
        logger.ReleaseFirstEntry.Set();
        await firstWrite;
        if (secondWrite is not null)
        {
            await secondWrite;
        }

        Assert.IsTrue(firstReachedDiagnostics, "The first accepted transition did not reach diagnostics.");
        Assert.IsTrue(secondMutationCompleted, "The second writer did not mutate while the first response was paused.");
        MapRoomCameraComponentState firstResponse = firstSender.Single<MapRoomCameraComponentState>();
        MapRoomCameraComponentState secondResponse = secondSender.Single<MapRoomCameraComponentState>();
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
        Assert.IsTrue(scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.RoomId, scenario.PlayerA, SimulationLockType.TRANSIENT));
        MapRoomCameraComponentStateProcessor processor = new(scenario.Ownership, scenario.EntityRegistry, scenario.Diagnostics);
        RecordingPacketSender sender = new();

        await processor.Process(
            new AuthProcessorContext(scenario.PlayerA, sender),
            new MapRoomCameraComponentState(ScannerRoomScenarioFixture.CameraAId, 100f, 400.01f));

        Assert.IsFalse(sender.Single<MapRoomCameraComponentState>().Granted);
        MapRoomCameraRecord record = scenario.Room.GetCameraRecord(ScannerRoomScenarioFixture.CameraAId)!;
        Assert.AreEqual(MapRoomCameraRecord.MAX_ENERGY, record.Energy);
        Assert.AreEqual(MapRoomCameraRecord.MAX_HEALTH, record.Health);
        Assert.AreEqual(0, record.ComponentRevision);
    }

    private static async Task<ScannerRoomScenarioFixture> CreateDockedScenarioAsync()
    {
        ScannerRoomScenarioFixture scenario = new();
        MapRoomCameraDock dock = await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true);
        Assert.IsTrue(dock.Granted);
        return scenario;
    }

    private sealed class RecordingPacketSender : IPacketSender
    {
        private readonly List<Packet> packets = [];
        private readonly object sync = new();

        internal int DirectPackets { get; private set; }
        internal int BroadcastPackets { get; private set; }

        public ValueTask SendPacketAsync<T>(T packet, SessionId sessionId) where T : Packet
        {
            lock (sync)
            {
                packets.Add(packet);
                DirectPackets++;
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask SendPacketToAllAsync<T>(T packet) where T : Packet
        {
            lock (sync)
            {
                packets.Add(packet);
                BroadcastPackets++;
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask SendPacketToOthersAsync<T>(T packet, SessionId excludedSessionId) where T : Packet =>
            throw new NotSupportedException();

        internal T Single<T>() where T : Packet => All<T>().Single();

        internal T[] All<T>() where T : Packet
        {
            lock (sync)
            {
                return packets.OfType<T>().ToArray();
            }
        }
    }

    private sealed class BlockingLogger : ILogger<ScannerRoomDiagnostics>, IDisposable
    {
        private int entries;

        internal ManualResetEventSlim FirstEntry { get; } = new();
        internal ManualResetEventSlim ReleaseFirstEntry { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (Interlocked.Increment(ref entries) == 1)
            {
                FirstEntry.Set();
                ReleaseFirstEntry.Wait(TimeSpan.FromSeconds(5));
            }
        }

        public void Dispose()
        {
            FirstEntry.Dispose();
            ReleaseFirstEntry.Dispose();
        }
    }
}
