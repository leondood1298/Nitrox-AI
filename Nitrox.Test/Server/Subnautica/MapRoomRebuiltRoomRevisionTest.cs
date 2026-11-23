using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;

namespace Nitrox.Test.Server.Subnautica;

/// <summary>
///     A Scanner Room that is deconstructed and built again keeps its id, and its default cameras get the same ids
///     again. Clients remember the highest docking, light and component revision they saw per id and discard anything
///     lower, so the new room and its camera records must not start their revisions over.
/// </summary>
[TestClass]
public sealed class MapRoomRebuiltRoomRevisionTest
{
    private static readonly NitroxId baseId = new("00000000-0000-0000-0000-000000000400");
    private static readonly NitroxId roomId = new("00000000-0000-0000-0000-000000000401");
    // A default camera that never left its dock: it has no entity of its own, only the room's record.
    private static readonly NitroxId defaultCameraId = new("00000000-0000-0000-0000-000000000402");
    private static readonly NitroxInt3 cell = new(1, 0, 2);

    [TestMethod]
    public async Task RoomBuiltAgainOnItsBaseContinuesItsDockingRevisions()
    {
        ScannerRoomScenarioFixture scenario = new();
        AddBase(scenario);
        MapRoomEntity firstRoom = await BuildRoomAsync(scenario, operationId: 1);
        Assert.AreEqual(1, (await DockDefaultCameraAsync(scenario, true)).Revision);
        Assert.AreEqual(2, (await DockDefaultCameraAsync(scenario, false)).Revision);
        Assert.AreEqual(3, (await DockDefaultCameraAsync(scenario, true)).Revision);
        await DeconstructAsync(scenario, firstRoom);

        MapRoomEntity rebuiltRoom = await BuildRoomAsync(scenario, operationId: 2);
        MapRoomCameraDock firstDock = await DockDefaultCameraAsync(scenario, true);

        Assert.AreNotSame(firstRoom, rebuiltRoom);
        Assert.IsTrue(firstDock.Granted);
        Assert.IsTrue(firstDock.Revision > 3, $"Players who saw revision 3 of this room id would discard revision {firstDock.Revision}.");
    }

    [TestMethod]
    public async Task DefaultCameraOfARoomBuiltAgainContinuesItsLightAndComponentRevisions()
    {
        ScannerRoomScenarioFixture scenario = new();
        AddBase(scenario);
        MapRoomEntity firstRoom = await BuildRoomAsync(scenario, operationId: 1);
        Assert.IsTrue((await DockDefaultCameraAsync(scenario, true)).Granted);
        MapRoomCameraRecord firstRecord = firstRoom.GetCameraRecord(defaultCameraId)!;
        firstRecord.LightRevision = 5;
        firstRecord.ComponentRevision = 40;
        firstRecord.Energy = 12f;
        await DeconstructAsync(scenario, firstRoom);

        MapRoomEntity rebuiltRoom = await BuildRoomAsync(scenario, operationId: 2);
        MapRoomCameraDock dock = await DockDefaultCameraAsync(scenario, true);

        MapRoomCameraRecord rebuiltRecord = rebuiltRoom.GetCameraRecord(defaultCameraId)!;
        Assert.AreNotSame(firstRecord, rebuiltRecord);
        Assert.AreEqual(MapRoomCameraRecord.MAX_ENERGY, rebuiltRecord.Energy, "It is a new camera.");
        Assert.IsTrue(rebuiltRecord.LightRevision > 5);
        Assert.IsTrue(rebuiltRecord.ComponentRevision > 40);
        Assert.AreEqual(rebuiltRecord.ComponentRevision, dock.ComponentRevision, "Players are told the continued revision.");
    }

    [TestMethod]
    public async Task CameraIdThatWasDestroyedContinuesItsRevisionsWhenItIsRegisteredAgain()
    {
        ScannerRoomScenarioFixture scenario = new();
        NitroxId cameraId = ScannerRoomScenarioFixture.CameraAId;
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        MapRoomCameraRecord record = scenario.Room.GetCameraRecord(cameraId)!;
        record.LightRevision = 2;
        record.ComponentRevision = 17;

        await scenario.DestroyAsync(scenario.PlayerA, cameraId);
        Assert.IsNull(scenario.Room.GetCameraRecord(cameraId));
        // The room's simulating player reports the camera in a dock again, which gives it a new record.
        Assert.IsTrue(scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.RoomId, scenario.PlayerA, SimulationLockType.TRANSIENT));
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true, establishAuthority: false)).Granted);
        MapRoomCameraRecord again = scenario.Room.GetCameraRecord(cameraId)!;

        Assert.AreEqual(3, again.LightRevision);
        Assert.AreEqual(18, again.ComponentRevision);
    }

    [TestMethod]
    public async Task IdsThatWereNeverRemovedStartAtZero()
    {
        ScannerRoomScenarioFixture scenario = new();
        MapRoomEntity room = new(roomId, baseId, cell);

        scenario.Manager.RoomBuilt(room);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);

        MapRoomCameraRecord record = scenario.Room.GetCameraRecord(ScannerRoomScenarioFixture.CameraAId)!;
        Assert.AreEqual(0, room.DockingRevision);
        Assert.AreEqual(0, record.LightRevision);
        Assert.AreEqual(0, record.ComponentRevision);
    }

    [TestMethod]
    public async Task FloorOnlyEverRises()
    {
        ScannerRoomScenarioFixture scenario = new();
        MapRoomEntity longLived = new(roomId, baseId, cell) { DockingRevision = 9 };
        MapRoomEntity shortLived = new(roomId, baseId, cell) { DockingRevision = 2 };
        await scenario.CleanupAsync(longLived);
        await scenario.CleanupAsync(shortLived);
        MapRoomEntity rebuilt = new(roomId, baseId, cell);

        scenario.Manager.RoomBuilt(rebuilt);

        Assert.AreEqual(10, rebuilt.DockingRevision);
    }

    private static void AddBase(ScannerRoomScenarioFixture scenario)
    {
        BuildEntity buildEntity = BuildEntity.MakeEmpty();
        buildEntity.Id = baseId;
        scenario.WorldEntityManager.AddOrUpdateGlobalRootEntity(buildEntity);
    }

    /// <summary>
    ///     Places the room's ghost on the base and finishes it. The room takes the ghost's id, every time.
    /// </summary>
    private static async Task<MapRoomEntity> BuildRoomAsync(ScannerRoomScenarioFixture scenario, int operationId)
    {
        GhostEntity ghost = GhostEntity.MakeEmpty();
        ghost.Id = roomId;
        ghost.ParentId = baseId;
        ghost.TechType = new NitroxTechType("BaseMapRoom");
        scenario.WorldEntityManager.AddOrUpdateGlobalRootEntity(ghost);

        MapRoomEntity builtRoom = new(roomId, baseId, cell);
        await scenario.UpdateBaseAsync(scenario.PlayerA, new UpdateBase(baseId, roomId, new BaseData(), builtRoom, [], [],
            new Dictionary<NitroxId, NitroxInt3> { [roomId] = cell }, (null, null), operationId));

        Assert.IsTrue(scenario.EntityRegistry.TryGetEntityById(roomId, out MapRoomEntity registeredRoom), "The room was not accepted.");
        Assert.AreSame(scenario.PlayerA, scenario.Ownership.GetPlayerForLock(roomId));
        return registeredRoom;
    }

    private static async Task<MapRoomCameraDock> DockDefaultCameraAsync(ScannerRoomScenarioFixture scenario, bool isDocked)
    {
        IReadOnlyList<Nitrox.Model.Packets.Packet> sent = await scenario.DockPacketsAsync(scenario.PlayerA,
            new MapRoomCameraDock(defaultCameraId, roomId, 0, isDocked: isDocked));
        return sent.OfType<MapRoomCameraDock>().Single();
    }

    private static async Task DeconstructAsync(ScannerRoomScenarioFixture scenario, MapRoomEntity room)
    {
        scenario.WorldEntityManager.RemoveGlobalRootEntity(room.Id);
        await scenario.CleanupAsync(room);
    }
}
