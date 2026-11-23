using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Test.Server.Subnautica;

/// <summary>
///     A camera's entity is replaced when it is picked up (an inventory item takes its place) and again when it is
///     dropped (a new world entity). The light, battery, health and their revisions of a camera that no Scanner Room has
///     a record for live on that entity, so they have to survive both replacements. Clients discard a revision lower
///     than one they already saw, so a counter that starts over would silence updates for everyone already connected.
/// </summary>
[TestClass]
public sealed class MapRoomCameraPickupStateTest
{
    private static readonly NitroxId cameraId = ScannerRoomScenarioFixture.CameraAId;

    [TestMethod]
    public async Task CameraWithoutARoomKeepsItsStateWhenPickedUp()
    {
        ScannerRoomScenarioFixture scenario = new();
        Entity(scenario).Metadata = new MapRoomCameraMetadata(false, 4, 37f, 250f, 9);

        await scenario.PickupAsync(scenario.PlayerA, cameraId);

        Assert.IsInstanceOfType<InventoryItemEntity>(Entity(scenario));
        AssertState(Entity(scenario), 4, 37f, 250f, 9);
    }

    [TestMethod]
    public async Task CameraWithoutARoomKeepsItsStateWhenDroppedAgain()
    {
        ScannerRoomScenarioFixture scenario = new();
        Entity(scenario).Metadata = new MapRoomCameraMetadata(false, 4, 37f, 250f, 9);
        await scenario.PickupAsync(scenario.PlayerA, cameraId);

        await scenario.DropAsync(scenario.PlayerA, cameraId);

        Assert.IsInstanceOfType<WorldEntity>(Entity(scenario));
        AssertState(Entity(scenario), 4, 37f, 250f, 9);
    }

    [TestMethod]
    public async Task StateSentByTheClientWithAPickupOrDropIsNotTrusted()
    {
        // Only the light and component packets, with their ownership checks, may change a camera's state.
        ScannerRoomScenarioFixture scenario = new();
        Entity(scenario).Metadata = new MapRoomCameraMetadata(false, 4, 37f, 250f, 9);
        MapRoomCameraMetadata forged = new(true, 99, MapRoomCameraRecord.MAX_ENERGY, MapRoomCameraRecord.MAX_HEALTH, 99);

        await scenario.PickupAsync(scenario.PlayerA, cameraId, forged);
        AssertState(Entity(scenario), 4, 37f, 250f, 9);

        await scenario.DropAsync(scenario.PlayerA, cameraId, forged);
        AssertState(Entity(scenario), 4, 37f, 250f, 9);
    }

    [TestMethod]
    public async Task CameraOfARoomKeepsItsRecordWhilePickedUpAndCarriesNoStateOfItsOwn()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        MapRoomCameraRecord record = scenario.Room.GetCameraRecord(cameraId)!;
        record.Energy = 12f;
        record.ComponentRevision = 6;

        await scenario.PickupAsync(scenario.PlayerA, cameraId);
        await scenario.DropAsync(scenario.PlayerA, cameraId);

        Assert.AreSame(record, scenario.Room.GetCameraRecord(cameraId));
        Assert.AreEqual(12f, record.Energy);
        Assert.IsNull(Entity(scenario).Metadata, "The room's record stays the only place this state lives.");
    }

    [TestMethod]
    public async Task CameraInAnInventoryKeepsItsStateWhenItsRoomIsDeconstructed()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        MapRoomCameraRecord record = scenario.Room.GetCameraRecord(cameraId)!;
        record.Energy = 12f;
        record.Health = 180f;
        record.ComponentRevision = 6;
        record.LightRevision = 3;
        await scenario.PickupAsync(scenario.PlayerB, cameraId);
        Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(ScannerRoomScenarioFixture.RoomId).HasValue);

        await scenario.CleanupAsync(scenario.Room);
        AssertState(Entity(scenario), 3, 12f, 180f, 6);

        await scenario.DropAsync(scenario.PlayerB, cameraId);
        AssertState(Entity(scenario), 3, 12f, 180f, 6);
    }

    private static Entity Entity(ScannerRoomScenarioFixture scenario)
    {
        Assert.IsTrue(scenario.EntityRegistry.TryGetEntityById(cameraId, out Entity camera));
        return camera;
    }

    private static void AssertState(Entity camera, long lightRevision, float energy, float health, long componentRevision)
    {
        Assert.IsInstanceOfType<MapRoomCameraMetadata>(camera.Metadata);
        MapRoomCameraMetadata state = (MapRoomCameraMetadata)camera.Metadata;
        Assert.AreEqual(lightRevision, state.LightRevision);
        Assert.AreEqual(energy, state.Energy);
        Assert.AreEqual(health, state.Health);
        Assert.AreEqual(componentRevision, state.ComponentRevision);
    }
}
