using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Serialization;
using Nitrox.Server.Subnautica.Models.Serialization.World;

namespace Nitrox.Test.Server.Serialization;

[TestClass]
public sealed class WorldServiceScannerRoomSnapshotTest
{
    private static readonly NitroxId BaseId = new("00000000-0000-0000-0000-000000000100");
    private static readonly NitroxId LowRoomId = new("00000000-0000-0000-0000-000000000201");
    private static readonly NitroxId HighRoomId = new("00000000-0000-0000-0000-000000000202");

    [TestMethod, Timeout(10_000)]
    public void GlobalRootSerializationBlocksMutationAndCapturesExactSavedState()
    {
        MapRoomEntity room = CreateRoom(LowRoomId);
        GlobalRootData globalRootData = CreateGlobalRootData(room);
        ServerJsonSerializer serializer = new(NullLogger<ServerJsonSerializer>.Instance);
        using MemoryStream payload = new();
        using ManualResetEventSlim serializationStarted = new();
        using ManualResetEventSlim mutationAttempted = new();
        using ManualResetEventSlim mutationCompleted = new();
        int acquiredImmediately = -1;
        int mutationApplied = 0;

        Task mutation = Task.Run(() =>
        {
            serializationStarted.Wait();
            bool lockTaken = Monitor.TryEnter(room);
            Volatile.Write(ref acquiredImmediately, lockTaken ? 1 : 0);
            mutationAttempted.Set();
            try
            {
                if (!lockTaken)
                {
                    Monitor.Enter(room, ref lockTaken);
                }
                if (room.TryApplyScanResult(room.ScanResultGeneration,
                        new MapRoomScanResultRecord("resource-after-save", new NitroxTechType("Quartz"),
                            new NitroxVector3(9f, 8f, 7f))))
                {
                    Volatile.Write(ref mutationApplied, 1);
                }
            }
            finally
            {
                if (lockTaken)
                {
                    Monitor.Exit(room);
                }
                mutationCompleted.Set();
            }
        });

        IReadOnlyList<SerializedScannerRoomState> captured =
            WorldService.SerializeGlobalRootDataWithScannerRoomSnapshot(
                () => [room],
                () =>
                {
                    serializationStarted.Set();
                    Assert.IsTrue(mutationAttempted.Wait(TimeSpan.FromSeconds(5)),
                        "The mutation task did not reach its lock attempt.");
                    Assert.AreEqual(0, Volatile.Read(ref acquiredImmediately),
                        "A Scanner Room mutation must not enter during GlobalRootData serialization.");
                    Assert.IsFalse(mutationCompleted.IsSet,
                        "The mutation must remain blocked until serialization and fingerprint capture finish.");
                    serializer.Serialize(payload, globalRootData);
                });

        Assert.IsTrue(mutation.Wait(TimeSpan.FromSeconds(5)), "The mutation did not resume after snapshot capture.");
        Assert.AreEqual(1, Volatile.Read(ref mutationApplied));
        Assert.IsTrue(mutationCompleted.IsSet);

        using MemoryStream input = new(payload.ToArray());
        GlobalRootData restored = serializer.Deserialize<GlobalRootData>(input);
        MapRoomEntity savedRoom = restored.Entities.Single().ChildEntities.OfType<MapRoomEntity>().Single();
        SerializedScannerRoomState savedState = captured.Single();
        ScannerRoomStateSnapshot savedPayloadSnapshot = ScannerRoomStateFingerprint.Create(savedRoom);
        ScannerRoomStateSnapshot liveSnapshot = ScannerRoomStateFingerprint.Create(room);

        Assert.IsNull(savedState.InvariantFailure);
        Assert.AreEqual(savedState.Snapshot.CanonicalState, savedPayloadSnapshot.CanonicalState);
        Assert.AreEqual(savedState.Snapshot.Fingerprint, savedPayloadSnapshot.Fingerprint);
        Assert.AreNotEqual(savedState.Snapshot.Fingerprint, liveSnapshot.Fingerprint,
            "The post-capture mutation must be live state only, not part of the saved payload.");

        ScannerRoomDiagnostics diagnostics = new(NullLogger<ScannerRoomDiagnostics>.Instance);
        ScannerRoomDiagnosticEntry checkpoint = diagnostics.RecordCapturedCheckpoint(
            "save", room, savedState.Snapshot, savedState.InvariantFailure, "serialized_snapshot");
        Assert.AreEqual(ScannerRoomDiagnosticOutcome.Checkpoint, checkpoint.Outcome);
        Assert.AreEqual(savedState.Snapshot.Fingerprint, checkpoint.StateFingerprint,
            "The save checkpoint must report the serialized fingerprint, not recompute current live state.");
    }

    [TestMethod, Timeout(10_000)]
    public void LockRetryMakesProgressAgainstInvertedDockTransferOrder()
    {
        MapRoomEntity lowRoom = CreateRoom(LowRoomId);
        MapRoomEntity highRoom = CreateRoom(HighRoomId);
        using ManualResetEventSlim highRoomHeld = new();
        using ManualResetEventSlim highRoomLockAttempted = new();
        using ManualResetEventSlim invertedHolderAcquiredLowRoom = new();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        IReadOnlyList<MapRoomEntity> orderedRooms = new SignalingRoomList(
            [lowRoom, highRoom],
            index =>
            {
                if (index == 1)
                {
                    highRoomLockAttempted.Set();
                }
            });

        Task<bool> invertedHolder = Task.Run(() =>
        {
            lock (highRoom)
            {
                highRoomHeld.Set();
                if (!highRoomLockAttempted.Wait(TimeSpan.FromSeconds(5)))
                {
                    return false;
                }
                lock (lowRoom)
                {
                    invertedHolderAcquiredLowRoom.Set();
                }
            }
            return true;
        });

        Assert.IsTrue(highRoomHeld.Wait(TimeSpan.FromSeconds(5)));
        Task lockAcquisition = Task.Run(() =>
        {
            WorldService.EnterScannerRoomLocks(orderedRooms, timeout.Token);
            try
            {
                Assert.IsTrue(Monitor.IsEntered(lowRoom));
                Assert.IsTrue(Monitor.IsEntered(highRoom));
            }
            finally
            {
                Monitor.Exit(highRoom);
                Monitor.Exit(lowRoom);
            }
        });

        Assert.IsTrue(invertedHolder.Wait(TimeSpan.FromSeconds(5)),
            "The inverted high-room to low-room holder deadlocked with save acquisition.");
        Assert.IsTrue(invertedHolder.Result);
        Assert.IsTrue(lockAcquisition.Wait(TimeSpan.FromSeconds(5)),
            "Ordered save acquisition did not make progress after the inversion drained.");
        Assert.IsTrue(highRoomLockAttempted.IsSet,
            "The regression must force save to encounter the busy high-ID room after locking the low-ID room.");
        Assert.IsTrue(invertedHolderAcquiredLowRoom.IsSet,
            "Save must release its partial lock set before retrying the busy high-ID room.");
    }

    [TestMethod, Timeout(10_000)]
    public void RoomMembershipChangeIsReconciledBeforeSerialization()
    {
        MapRoomEntity firstRoom = CreateRoom(LowRoomId);
        MapRoomEntity addedRoom = CreateRoom(HighRoomId);
        int providerCalls = 0;
        bool bothRoomsLockedDuringSerialization = false;

        IReadOnlyList<SerializedScannerRoomState> captured =
            WorldService.SerializeGlobalRootDataWithScannerRoomSnapshot(
                () => Interlocked.Increment(ref providerCalls) == 1 ? [firstRoom] : [addedRoom, firstRoom],
                () =>
                {
                    Task<bool> lockProbe = Task.Run(() =>
                    {
                        bool firstTaken = Monitor.TryEnter(firstRoom);
                        bool addedTaken = Monitor.TryEnter(addedRoom);
                        if (addedTaken)
                        {
                            Monitor.Exit(addedRoom);
                        }
                        if (firstTaken)
                        {
                            Monitor.Exit(firstRoom);
                        }
                        return !firstTaken && !addedTaken;
                    });
                    Assert.IsTrue(lockProbe.Wait(TimeSpan.FromSeconds(5)));
                    bothRoomsLockedDuringSerialization = lockProbe.Result;
                });

        Assert.IsTrue(bothRoomsLockedDuringSerialization);
        Assert.AreEqual(2, captured.Count);
        Assert.IsTrue(providerCalls >= 4,
            "Membership must be read before and after lock acquisition, then retried after a change.");
    }

    [TestMethod]
    public void CapturedCheckpointRetainsCapturedInvariantAfterLiveRepair()
    {
        MapRoomEntity room = CreateRoom(LowRoomId);
        room.RightDockCameraId = room.LeftDockCameraId;
        ScannerRoomStateSnapshot snapshot = ScannerRoomStateFingerprint.Create(room);
        string? invariantFailure = ScannerRoomStateFingerprint.Validate(room);
        room.RightDockCameraId = null;

        ScannerRoomDiagnostics diagnostics = new(NullLogger<ScannerRoomDiagnostics>.Instance);
        ScannerRoomDiagnosticEntry checkpoint = diagnostics.RecordCapturedCheckpoint(
            "save", room, snapshot, invariantFailure, "serialized_snapshot");

        Assert.AreEqual(ScannerRoomDiagnosticOutcome.InvariantFailure, checkpoint.Outcome);
        Assert.AreEqual("save_invalid", checkpoint.EventName);
        Assert.AreEqual("duplicate_dock", checkpoint.Reason);
        Assert.AreEqual(snapshot.Fingerprint, checkpoint.StateFingerprint);
    }

    private static GlobalRootData CreateGlobalRootData(params MapRoomEntity[] rooms)
    {
        GlobalRootEntity root = new(
            new NitroxTransform(NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxVector3.One),
            GlobalRootEntity.GLOBAL_ROOT_LEVEL,
            "scanner-room-test-root",
            true,
            BaseId,
            new NitroxTechType("Base"),
            null,
            null,
            rooms.Cast<Entity>().ToList());
        return GlobalRootData.From([root]);
    }

    private static MapRoomEntity CreateRoom(NitroxId roomId)
    {
        NitroxId cameraId = roomId.Increment();
        return new MapRoomEntity(
            new NitroxInt3(1, 2, 3),
            cameraId,
            null,
            4,
            [new MapRoomCameraRecord(cameraId, 1, true, 2, 80f, 90f, 3)],
            5,
            6,
            [new MapRoomScanResultRecord("resource-before-save", new NitroxTechType("Gold"), new NitroxVector3(1f, 2f, 3f))],
            7,
            [new NitroxTechType("Gold")],
            null,
            new NitroxTransform(new NitroxVector3(4f, 5f, 6f), NitroxQuaternion.Identity, NitroxVector3.One),
            GlobalRootEntity.GLOBAL_ROOT_LEVEL,
            "scanner-room",
            true,
            roomId,
            new NitroxTechType("MapRoom"),
            new MapRoomMetadata(new NitroxTechType("Gold"), 1, 5, 6),
            BaseId,
            []);
    }

    private sealed class SignalingRoomList(MapRoomEntity[] rooms, Action<int> onRead) : IReadOnlyList<MapRoomEntity>
    {
        public MapRoomEntity this[int index]
        {
            get
            {
                onRead(index);
                return rooms[index];
            }
        }

        public int Count => rooms.Length;

        public IEnumerator<MapRoomEntity> GetEnumerator() => ((IEnumerable<MapRoomEntity>)rooms).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => rooms.GetEnumerator();
    }
}
