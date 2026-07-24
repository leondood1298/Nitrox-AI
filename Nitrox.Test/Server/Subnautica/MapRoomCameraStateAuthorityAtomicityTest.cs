using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
public sealed class MapRoomCameraStateAuthorityAtomicityTest
{
    [TestMethod]
    public async Task LightStateIsQueuedBeforeCameraOwnershipCanTransfer()
    {
        ScannerRoomScenarioFixture scenario = await CreateDockedScenarioAsync();
        Assert.IsTrue(scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.CameraAId, scenario.PlayerA,
            SimulationLockType.EXCLUSIVE));
        MapRoomCameraLightProcessor processor = new(scenario.Ownership, scenario.EntityRegistry, scenario.Diagnostics);
        BlockingBroadcastPacketSender sender = new();

        Task process = Task.Run(() => processor.Process(new AuthProcessorContext(scenario.PlayerA, sender),
            new MapRoomCameraLight(ScannerRoomScenarioFixture.CameraAId, true)));
        Assert.IsTrue(sender.BroadcastEntered.Wait(TimeSpan.FromSeconds(5)), "Accepted light state never reached the enqueue boundary.");

        using ManualResetEventSlim transferStarted = new();
        Task<bool> transfer = Task.Run(() =>
        {
            transferStarted.Set();
            return scenario.Ownership.RevokeIfOwner(ScannerRoomScenarioFixture.CameraAId, scenario.PlayerA);
        });
        Assert.IsTrue(transferStarted.Wait(TimeSpan.FromSeconds(5)));
        bool transferWasBlocked = await RemainBlockedUntilBroadcastReleased(sender, process, transfer);

        Assert.IsTrue(transferWasBlocked, "Camera ownership changed before the accepted light state was queued.");
        Assert.IsTrue(await transfer);
        Assert.IsTrue(scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.CameraAId, scenario.PlayerB,
            SimulationLockType.EXCLUSIVE));

        MapRoomCameraRecord record = scenario.Room.GetCameraRecord(ScannerRoomScenarioFixture.CameraAId)!;
        Assert.IsTrue(record.LightOn);
        Assert.AreEqual(1, record.LightRevision);
        MapRoomCameraLight accepted = sender.All<MapRoomCameraLight>().Single(packet => packet.Granted);
        Assert.IsTrue(accepted.On);
        Assert.AreEqual(1, accepted.Revision);

        await processor.Process(new AuthProcessorContext(scenario.PlayerA, sender),
            new MapRoomCameraLight(ScannerRoomScenarioFixture.CameraAId, false));

        MapRoomCameraLight rejected = sender.All<MapRoomCameraLight>().Single(packet => !packet.Granted);
        Assert.IsFalse(rejected.Granted);
        Assert.IsTrue(record.LightOn, "A delayed former-owner packet changed canonical light state.");
        Assert.AreEqual(1, record.LightRevision);
    }

    [TestMethod]
    public async Task ComponentStateIsQueuedBeforeCameraOwnershipCanTransfer()
    {
        ScannerRoomScenarioFixture scenario = await CreateDockedScenarioAsync();
        Assert.IsTrue(scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.CameraAId, scenario.PlayerA,
            SimulationLockType.EXCLUSIVE));
        MapRoomCameraComponentStateProcessor processor = new(scenario.Ownership, scenario.EntityRegistry, scenario.Diagnostics);
        BlockingBroadcastPacketSender sender = new();

        Task process = Task.Run(() => processor.Process(new AuthProcessorContext(scenario.PlayerA, sender),
            new MapRoomCameraComponentState(ScannerRoomScenarioFixture.CameraAId, 75f, 50f)));
        Assert.IsTrue(sender.BroadcastEntered.Wait(TimeSpan.FromSeconds(5)), "Accepted component state never reached the enqueue boundary.");

        using ManualResetEventSlim transferStarted = new();
        Task<bool> transfer = Task.Run(() =>
        {
            transferStarted.Set();
            return scenario.Ownership.RevokeIfOwner(ScannerRoomScenarioFixture.CameraAId, scenario.PlayerA);
        });
        Assert.IsTrue(transferStarted.Wait(TimeSpan.FromSeconds(5)));
        bool transferWasBlocked = await RemainBlockedUntilBroadcastReleased(sender, process, transfer);

        Assert.IsTrue(transferWasBlocked, "Camera ownership changed before the accepted component state was queued.");
        Assert.IsTrue(await transfer);
        Assert.IsTrue(scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.CameraAId, scenario.PlayerB,
            SimulationLockType.EXCLUSIVE));

        MapRoomCameraRecord record = scenario.Room.GetCameraRecord(ScannerRoomScenarioFixture.CameraAId)!;
        Assert.AreEqual(75f, record.Energy);
        Assert.AreEqual(50f, record.Health);
        Assert.AreEqual(1, record.ComponentRevision);
        MapRoomCameraComponentState accepted = sender.All<MapRoomCameraComponentState>().Single(packet => packet.Granted);
        Assert.AreEqual(75f, accepted.Energy);
        Assert.AreEqual(50f, accepted.Health);
        Assert.AreEqual(1, accepted.Revision);

        await processor.Process(new AuthProcessorContext(scenario.PlayerA, sender),
            new MapRoomCameraComponentState(ScannerRoomScenarioFixture.CameraAId, 5f, 5f));

        MapRoomCameraComponentState rejected = sender.All<MapRoomCameraComponentState>().Single(packet => !packet.Granted);
        Assert.IsFalse(rejected.Granted);
        Assert.AreEqual(75f, record.Energy, "A delayed former-owner packet changed canonical energy.");
        Assert.AreEqual(50f, record.Health, "A delayed former-owner packet changed canonical health.");
        Assert.AreEqual(1, record.ComponentRevision);
    }

    [TestMethod]
    public async Task DockedRoomComponentStateIsQueuedBeforeRoomOrCameraAuthorityCanTransfer()
    {
        ScannerRoomScenarioFixture scenario = await CreateDockedScenarioAsync();
        Assert.IsFalse(scenario.Ownership.TryGetLock(ScannerRoomScenarioFixture.CameraAId, out _));
        Assert.IsTrue(scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.RoomId, scenario.PlayerA,
            SimulationLockType.TRANSIENT));
        MapRoomCameraComponentStateProcessor processor = new(scenario.Ownership, scenario.EntityRegistry, scenario.Diagnostics);
        BlockingBroadcastPacketSender sender = new();

        Task process = Task.Run(() => processor.Process(new AuthProcessorContext(scenario.PlayerA, sender),
            new MapRoomCameraComponentState(ScannerRoomScenarioFixture.CameraAId, 60f, 80f)));
        Assert.IsTrue(sender.BroadcastEntered.Wait(TimeSpan.FromSeconds(5)), "Docked-room component state never reached the enqueue boundary.");

        using ManualResetEventSlim transferStarted = new();
        Task<(bool RoomRevoked, bool CameraAcquired)> transfer = Task.Run(() =>
        {
            transferStarted.Set();
            bool roomRevoked = scenario.Ownership.RevokeIfOwner(ScannerRoomScenarioFixture.RoomId, scenario.PlayerA);
            bool cameraAcquired = scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.CameraAId, scenario.PlayerB,
                SimulationLockType.EXCLUSIVE);
            return (roomRevoked, cameraAcquired);
        });
        Assert.IsTrue(transferStarted.Wait(TimeSpan.FromSeconds(5)));
        bool transferWasBlocked = await RemainBlockedUntilBroadcastReleased(sender, process, transfer);

        Assert.IsTrue(transferWasBlocked, "Docked-room or camera authority changed before component state was queued.");
        (bool roomRevoked, bool cameraAcquired) = await transfer;
        Assert.IsTrue(roomRevoked);
        Assert.IsTrue(cameraAcquired);

        MapRoomCameraRecord record = scenario.Room.GetCameraRecord(ScannerRoomScenarioFixture.CameraAId)!;
        Assert.AreEqual(60f, record.Energy);
        Assert.AreEqual(80f, record.Health);
        Assert.AreEqual(1, record.ComponentRevision);

        await processor.Process(new AuthProcessorContext(scenario.PlayerA, sender),
            new MapRoomCameraComponentState(ScannerRoomScenarioFixture.CameraAId, 10f, 10f));

        Assert.AreEqual(1, sender.All<MapRoomCameraComponentState>().Count(packet => !packet.Granted));
        Assert.AreEqual(60f, record.Energy, "A former room owner changed canonical energy after authority transfer.");
        Assert.AreEqual(80f, record.Health, "A former room owner changed canonical health after authority transfer.");
        Assert.AreEqual(1, record.ComponentRevision);
    }

    private static async Task<ScannerRoomScenarioFixture> CreateDockedScenarioAsync()
    {
        ScannerRoomScenarioFixture scenario = new();
        MapRoomCameraDock dock = await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true);
        Assert.IsTrue(dock.Granted);
        return scenario;
    }

    private static async Task<bool> RemainBlockedUntilBroadcastReleased(BlockingBroadcastPacketSender sender,
        Task process, Task transfer)
    {
        Task completed = await Task.WhenAny(transfer, Task.Delay(TimeSpan.FromMilliseconds(250)));
        bool transferWasBlocked = completed != transfer;
        sender.ReleaseBroadcast.Set();
        await Task.WhenAll(process, transfer);
        return transferWasBlocked;
    }

    private sealed class BlockingBroadcastPacketSender : IPacketSender
    {
        private readonly ConcurrentQueue<Packet> packets = new();

        internal ManualResetEventSlim BroadcastEntered { get; } = new();
        internal ManualResetEventSlim ReleaseBroadcast { get; } = new();

        public ValueTask SendPacketAsync<T>(T packet, SessionId sessionId) where T : Packet
        {
            packets.Enqueue(packet);
            return ValueTask.CompletedTask;
        }

        public ValueTask SendPacketToAllAsync<T>(T packet) where T : Packet
        {
            BroadcastEntered.Set();
            if (!ReleaseBroadcast.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Test did not release the blocked Scanner Room broadcast.");
            }
            packets.Enqueue(packet);
            return ValueTask.CompletedTask;
        }

        public ValueTask SendPacketToOthersAsync<T>(T packet, SessionId excludedSessionId) where T : Packet =>
            throw new NotSupportedException();

        internal T[] All<T>() where T : Packet => packets.OfType<T>().ToArray();
    }
}
