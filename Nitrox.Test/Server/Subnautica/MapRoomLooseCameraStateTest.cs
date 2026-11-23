using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Models.Packets.Processors;

namespace Nitrox.Test.Server.Subnautica;

/// <summary>
///     Cameras that no Scanner Room has a record for: fabricated and never docked, dropped after a pickup, or left
///     behind when their room was removed.
/// </summary>
[TestClass]
public sealed class MapRoomLooseCameraStateTest
{
    private static readonly NitroxId cameraId = ScannerRoomScenarioFixture.CameraAId;

    [TestMethod]
    public async Task BatteryAndHealthOfUnregisteredCameraAreStoredOnTheCameraAndBroadcast()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsNull(scenario.Room.GetCameraRecord(cameraId));
        Assert.IsTrue(scenario.Ownership.TryAcquire(cameraId, scenario.PlayerA, SimulationLockType.TRANSIENT));
        MapRoomCameraComponentStateProcessor processor = new(scenario.Manager);

        await processor.Process(scenario.Context(scenario.PlayerA), new MapRoomCameraComponentState(cameraId, 42f, 310f));
        await processor.Process(scenario.Context(scenario.PlayerA), new MapRoomCameraComponentState(cameraId, 42f, 310f));
        await processor.Process(scenario.Context(scenario.PlayerA), new MapRoomCameraComponentState(cameraId, 41f, 310f));

        MapRoomCameraComponentState[] broadcasts = scenario.SentToAll<MapRoomCameraComponentState>().ToArray();
        Assert.AreEqual(3, broadcasts.Length);
        Assert.IsTrue(broadcasts.All(packet => packet.IsServerResponse && packet.Granted));
        CollectionAssert.AreEqual(new long[] { 1, 1, 2 }, broadcasts.Select(packet => packet.Revision).ToArray(),
            "The revision advances only when a value changes.");
        MapRoomCameraMetadata stored = (MapRoomCameraMetadata)Camera(scenario).Metadata;
        Assert.AreEqual(41f, stored.Energy);
        Assert.AreEqual(310f, stored.Health);
        Assert.AreEqual(2, stored.ComponentRevision);
        Assert.AreEqual(0, scenario.SentTo<MapRoomCameraComponentState>(scenario.PlayerA).Count);
    }

    [TestMethod]
    public async Task PlayerWhoDoesNotSimulateTheCameraCannotSetItsState()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue(scenario.Ownership.TryAcquire(cameraId, scenario.PlayerA, SimulationLockType.TRANSIENT));
        MapRoomCameraComponentStateProcessor processor = new(scenario.Manager);

        await processor.Process(scenario.Context(scenario.PlayerB), new MapRoomCameraComponentState(cameraId, 5f, 5f));

        Assert.IsFalse(scenario.SentTo<MapRoomCameraComponentState>(scenario.PlayerB).Single().Granted);
        Assert.AreEqual(0, scenario.SentToAll<MapRoomCameraComponentState>().Count);
        Assert.IsNull(Camera(scenario).Metadata);
    }

    [TestMethod]
    public async Task LightOfUnregisteredCameraNeedsControlAndIsStoredOnTheCamera()
    {
        ScannerRoomScenarioFixture scenario = new();
        MapRoomCameraLightProcessor processor = new(scenario.Manager);
        AuthProcessorContext context = scenario.Context(scenario.PlayerA);

        await processor.Process(context, new MapRoomCameraLight(cameraId, true));
        Assert.IsFalse(scenario.SentTo<MapRoomCameraLight>(scenario.PlayerA).Single().Granted, "Without control the light cannot be changed.");
        Assert.IsNull(Camera(scenario).Metadata);

        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerA, cameraId, true)).Granted);
        await processor.Process(context, new MapRoomCameraLight(cameraId, true));
        await processor.Process(context, new MapRoomCameraLight(cameraId, true));
        await processor.Process(context, new MapRoomCameraLight(cameraId, false));

        MapRoomCameraLight[] broadcasts = scenario.SentToAll<MapRoomCameraLight>().ToArray();
        CollectionAssert.AreEqual(new[] { true, true, false }, broadcasts.Select(packet => packet.On).ToArray());
        CollectionAssert.AreEqual(new long[] { 1, 1, 2 }, broadcasts.Select(packet => packet.Revision).ToArray());
        MapRoomCameraMetadata stored = (MapRoomCameraMetadata)Camera(scenario).Metadata;
        Assert.IsFalse(stored.LightOn);
        Assert.AreEqual(2, stored.LightRevision);
    }

    [TestMethod]
    public async Task FirstDockMovesTheCamerasOwnStateIntoTheRoomRecord()
    {
        ScannerRoomScenarioFixture scenario = new();
        Camera(scenario).Metadata = new MapRoomCameraMetadata(true, 4, 37f, 250f, 9);

        MapRoomCameraDock response = await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true);

        Assert.IsTrue(response.Granted);
        MapRoomCameraRecord record = scenario.Room.GetCameraRecord(cameraId)!;
        Assert.IsTrue(record.LightOn);
        Assert.AreEqual(4, record.LightRevision);
        Assert.AreEqual(37f, record.Energy);
        Assert.AreEqual(250f, record.Health);
        Assert.AreEqual(9, record.ComponentRevision, "Revisions carry over so clients accept the updates that follow.");
        Assert.AreEqual(37f, response.Energy, "Everyone is told the camera's real battery level, not a full one.");
        Assert.IsNull(Camera(scenario).Metadata, "The record is now the only place this state lives.");
    }

    [TestMethod]
    public async Task CameraOutInTheWorldKeepsItsStateWhenItsRoomIsDeconstructed()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, false)).Granted);
        MapRoomCameraRecord record = scenario.Room.GetCameraRecord(cameraId)!;
        record.Energy = 12f;
        record.Health = 180f;
        record.ComponentRevision = 6;
        record.LightOn = true;
        record.LightRevision = 3;
        Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(ScannerRoomScenarioFixture.RoomId).HasValue);

        await scenario.CleanupAsync(scenario.Room);

        MapRoomCameraMetadata stored = (MapRoomCameraMetadata)Camera(scenario).Metadata;
        Assert.AreEqual(12f, stored.Energy);
        Assert.AreEqual(180f, stored.Health);
        Assert.AreEqual(6, stored.ComponentRevision);
        Assert.IsTrue(stored.LightOn);
        Assert.AreEqual(3, stored.LightRevision);
    }

    private static WorldEntity Camera(ScannerRoomScenarioFixture scenario)
    {
        Assert.IsTrue(scenario.EntityRegistry.TryGetEntityById(cameraId, out WorldEntity camera));
        return camera;
    }
}
