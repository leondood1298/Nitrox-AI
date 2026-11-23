using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Test.Server.Subnautica;
using Packet = Nitrox.Model.Packets.Packet;

namespace Nitrox.Test.Server.GameLogic;

/// <summary>
///     A camera keeps its light, battery, health and number on its own entity. Its entity changes shape with where the
///     camera is: a child of a room while docked, an entity of the world while loose, an inventory item while carried.
///     The state has to come through every one of these changes, and only the right player may change it.
/// </summary>
[TestClass]
public sealed class ScannerRoomManagerCameraStateTest
{
    private static readonly NitroxId cameraId = ScannerRoomScenarioFixture.CameraAId;

    [TestMethod]
    [DataRow(0f, 0f, true)]
    [DataRow(100f, 400f, true)]
    [DataRow(-0.01f, 400f, false)]
    [DataRow(100.01f, 400f, false)]
    [DataRow(100f, -0.01f, false)]
    [DataRow(100f, 400.01f, false)]
    [DataRow(float.NaN, 400f, false)]
    [DataRow(float.PositiveInfinity, 400f, false)]
    [DataRow(100f, float.NaN, false)]
    [DataRow(100f, float.NegativeInfinity, false)]
    public void BatteryAndHealthMustBeWithinWhatACameraCanHold(float energy, float health, bool expected)
    {
        Assert.AreEqual(expected, ScannerRoomManager.IsValidComponentState(energy, health));
    }

    [TestMethod]
    [DataRow(100f, 100, 400f, 100)]
    [DataRow(75.01f, 100, 300.01f, 100)]
    [DataRow(75f, 75, 300f, 75)]
    [DataRow(50f, 50, 200f, 50)]
    [DataRow(25f, 25, 100f, 25)]
    [DataRow(10f, 10, 40f, 10)]
    [DataRow(0f, 0, 0f, 0)]
    public void BatteryAndHealthAreLoggedInBandsOfTheirMaximum(float energy, int expectedEnergyBand, float health, int expectedHealthBand)
    {
        Assert.AreEqual(expectedEnergyBand, ScannerRoomManager.GetEnergyBand(energy));
        Assert.AreEqual(expectedHealthBand, ScannerRoomManager.GetHealthBand(health));
    }

    [TestMethod]
    public void NewCameraIsFullIntactDarkAndInNoDock()
    {
        MapRoomCameraMetadata state = new();

        Assert.AreEqual((false, MapRoomCameraMetadata.MAX_ENERGY, MapRoomCameraMetadata.MAX_HEALTH, 0, MapRoomCameraMetadata.NOT_DOCKED),
            (state.LightOn, state.Energy, state.Health, state.CameraNumber, state.DockingIndex));
        Assert.IsFalse(state.IsDocked);
    }

    [TestMethod]
    public async Task DockingAndUndockingChangeNothingOfACameraButItsDock()
    {
        ScannerRoomScenarioFixture scenario = new();
        scenario.SetCameraState(cameraId, new MapRoomCameraMetadata(true, 4, 37f, 250f, 9, 1));

        MapRoomCameraMetadata docked = (await scenario.DockAsync(scenario.PlayerA, cameraId, 1, true)).State;
        MapRoomCameraMetadata undocked = (await scenario.DockAsync(scenario.PlayerA, cameraId, 1, false)).State;

        Assert.AreEqual(new MapRoomCameraMetadata(true, 4, 37f, 250f, 9, 1, 1).ToString(), docked.ToString());
        Assert.AreEqual(new MapRoomCameraMetadata(true, 4, 37f, 250f, 9, 1).ToString(), undocked.ToString());
    }

    [TestMethod]
    public async Task LooseCameraTakesItsStateIntoAnInventoryAndBackIntoTheWorld()
    {
        ScannerRoomScenarioFixture scenario = new();
        scenario.SetCameraState(cameraId, new MapRoomCameraMetadata(false, 4, 37f, 250f, 9, 1));

        await scenario.PickupAsync(scenario.PlayerA, cameraId);

        Assert.IsInstanceOfType<InventoryItemEntity>(Camera(scenario));
        Assert.AreEqual(new MapRoomCameraMetadata(false, 4, 37f, 250f, 9, 1).ToString(), scenario.CameraState(cameraId).ToString());

        await scenario.DropAsync(scenario.PlayerA, cameraId);

        Assert.IsInstanceOfType<GlobalRootEntity>(Camera(scenario));
        Assert.AreEqual(new MapRoomCameraMetadata(false, 4, 37f, 250f, 9, 1).ToString(), scenario.CameraState(cameraId).ToString());
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted, "The camera that was dropped is a camera like any other.");
        Assert.AreEqual(new MapRoomCameraMetadata(false, 4, 37f, 250f, 9, 1, 0).ToString(), scenario.CameraState(cameraId).ToString());
    }

    [TestMethod]
    public async Task CameraPickedOutOfItsDockLeavesTheDockAndKeepsItsState()
    {
        ScannerRoomScenarioFixture scenario = new();
        scenario.SetCameraState(cameraId, new MapRoomCameraMetadata(false, 4, 37f, 250f, 9, 1));
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);

        // The game takes a docked camera out of its dock when it is picked up; the player's own report of that may come later.
        IReadOnlyList<Packet> sent = await scenario.PickupAsync(scenario.PlayerB, cameraId);
        ScannerRoomScenarioFixture.DockResult lateUndock = await scenario.DockAsync(scenario.PlayerB, cameraId, 0, false, establishAuthority: false);

        Assert.AreEqual(0, scenario.Room.ChildEntities.Count);
        Assert.IsInstanceOfType<InventoryItemEntity>(Camera(scenario));
        Assert.AreEqual(scenario.PlayerB.GameObjectId, Camera(scenario).ParentId);
        Assert.AreEqual(new MapRoomCameraMetadata(false, 4, 37f, 250f, 9, 1).ToString(), scenario.CameraState(cameraId).ToString());
        MapRoomCameraUndocked undocked = scenario.SentToAll<MapRoomCameraUndocked>().Single();
        Assert.AreEqual((cameraId, scenario.Room.Id), (undocked.CameraId, undocked.MapRoomId));
        List<Packet> order = [.. sent];
        Assert.IsTrue(order.IndexOf(undocked) < order.FindIndex(packet => packet is SpawnEntities), "Players take the camera out of the dock before they replace it with the inventory item.");
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(cameraId), "Nobody simulates a camera in an inventory.");
        Assert.IsFalse(scenario.WorldEntityManager.GetGlobalRootEntities().Any(entity => entity.Id == cameraId));
        Assert.IsFalse(lateUndock.Granted, "A camera in an inventory is in no room.");
        Assert.AreEqual(0, lateUndock.Sent.Count);
    }

    [TestMethod]
    public async Task StateAClientSendsWithAPickupOrADropIsNotTrusted()
    {
        // The client builds the entity that takes the camera's place. Whatever it puts there, the server's state moves over.
        ScannerRoomScenarioFixture scenario = new();
        scenario.SetCameraState(cameraId, new MapRoomCameraMetadata(false, 4, 37f, 250f, 9, 1));
        MapRoomCameraMetadata forged = new(true, 99, MapRoomCameraMetadata.MAX_ENERGY, MapRoomCameraMetadata.MAX_HEALTH, 99, 77, 1);

        await scenario.PickupAsync(scenario.PlayerA, cameraId, forged);
        Assert.AreEqual(new MapRoomCameraMetadata(false, 4, 37f, 250f, 9, 1).ToString(), scenario.CameraState(cameraId).ToString());

        IReadOnlyList<Packet> sent = await scenario.DropAsync(scenario.PlayerA, cameraId, forged);
        Assert.AreEqual(new MapRoomCameraMetadata(false, 4, 37f, 250f, 9, 1).ToString(), scenario.CameraState(cameraId).ToString());
        Assert.AreEqual(0, scenario.Room.ChildEntities.Count, "A camera gets into a dock by a dock report only.");
        EntityMetadataUpdate told = sent.OfType<EntityMetadataUpdate>().Single();
        Assert.AreSame(scenario.CameraState(cameraId), told.NewValue, "The client that built the entity is told what the camera's state really is.");
    }

    [TestMethod]
    public async Task PickingUpALitCameraSwitchesItsLightOffForEveryone()
    {
        // The game switches the light off on pickup (MapRoomCamera.OnPickedUp). The stored state has to follow.
        ScannerRoomScenarioFixture scenario = new();
        scenario.SetCameraState(cameraId, new MapRoomCameraMetadata(true, 4, 37f, 250f, 9, 1));

        IReadOnlyList<Packet> sent = await scenario.PickupAsync(scenario.PlayerA, cameraId);

        MapRoomCameraMetadata state = scenario.CameraState(cameraId);
        Assert.IsFalse(state.LightOn);
        Assert.AreEqual(37f, state.Energy, "Nothing else changes.");
        MapRoomCameraLight announced = sent.OfType<MapRoomCameraLight>().Single();
        Assert.AreSame(announced, scenario.SentToAll<MapRoomCameraLight>().Single());
        Assert.AreEqual((false, state.LightRevision, true, true), (announced.On, announced.Revision, announced.IsServerResponse, announced.Granted));
        Assert.AreEqual(5, announced.Revision);
    }

    [TestMethod]
    public async Task PickingUpADarkCameraAnnouncesNoLightChange()
    {
        ScannerRoomScenarioFixture scenario = new();
        scenario.SetCameraState(cameraId, new MapRoomCameraMetadata(false, 4, 37f, 250f, 9, 1));

        IReadOnlyList<Packet> sent = await scenario.PickupAsync(scenario.PlayerA, cameraId);

        Assert.AreEqual(0, sent.OfType<MapRoomCameraLight>().Count());
        Assert.AreEqual(4, scenario.CameraState(cameraId).LightRevision);
    }

    [TestMethod]
    public async Task DockedCameraIsRechargedByThePlayerWhoSimulatesItWhoeverSimulatesTheRoom()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        Assert.IsTrue(scenario.Ownership.TryAcquire(scenario.Room.Id, scenario.PlayerB, SimulationLockType.TRANSIENT));
        scenario.SetCameraComponents(cameraId, 20f, 250f);
        MapRoomCameraMetadata before = scenario.CameraState(cameraId);

        IReadOnlyList<Packet> fromRoomSimulator = await scenario.ComponentsAsync(scenario.PlayerB, new MapRoomCameraComponentState(cameraId, 90f, 250f));

        MapRoomCameraComponentState refusal = (MapRoomCameraComponentState)fromRoomSimulator.Single();
        Assert.IsFalse(refusal.Granted);
        Assert.AreSame(refusal, scenario.SentTo<MapRoomCameraComponentState>(scenario.PlayerB).Single());
        Assert.AreSame(before, scenario.CameraState(cameraId));

        IReadOnlyList<Packet> fromCameraSimulator = await scenario.ComponentsAsync(scenario.PlayerA, new MapRoomCameraComponentState(cameraId, 30f, 250f));

        MapRoomCameraComponentState accepted = (MapRoomCameraComponentState)fromCameraSimulator.Single();
        Assert.AreSame(accepted, scenario.SentToAll<MapRoomCameraComponentState>().Single());
        Assert.AreEqual((30f, 250f, true), (accepted.Energy, accepted.Health, accepted.Granted));
        Assert.AreEqual(30f, scenario.CameraState(cameraId).Energy);
        Assert.AreEqual(before.ComponentRevision + 1, accepted.Revision);
    }

    [TestMethod]
    public async Task ControllerOfADockedCameraIsTheOneWhoReportsItsBatteryAndHealth()
    {
        // Taking control makes the controller the camera's simulating player. The player who simulated it before
        // must not go on recharging it against the controller's reports.
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB, cameraId, 0, true)).Granted);

        IReadOnlyList<Packet> fromFormerSimulator = await scenario.ComponentsAsync(scenario.PlayerA, new MapRoomCameraComponentState(cameraId, 90f, 400f));
        IReadOnlyList<Packet> fromController = await scenario.ComponentsAsync(scenario.PlayerB, new MapRoomCameraComponentState(cameraId, 80f, 400f));

        Assert.IsFalse(((MapRoomCameraComponentState)fromFormerSimulator.Single()).Granted);
        Assert.IsTrue(((MapRoomCameraComponentState)fromController.Single()).Granted);
        Assert.AreEqual(80f, scenario.CameraState(cameraId).Energy);

        // After the release the controller goes on simulating the camera, in its dock or out of it.
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB, cameraId, 0, false)).Granted);
        Assert.IsTrue(((MapRoomCameraComponentState)(await scenario.ComponentsAsync(scenario.PlayerB, new MapRoomCameraComponentState(cameraId, 85f, 400f))).Single()).Granted);
    }

    [TestMethod]
    public async Task BatteryOrHealthACameraCannotHaveIsRefusedAndChangesNothing()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        MapRoomCameraMetadata before = scenario.CameraState(cameraId);

        IReadOnlyList<Packet> sent = await scenario.ComponentsAsync(scenario.PlayerA, new MapRoomCameraComponentState(cameraId, 100.5f, 400f));

        Assert.IsFalse(((MapRoomCameraComponentState)sent.Single()).Granted);
        Assert.AreSame(before, scenario.CameraState(cameraId));
        Assert.AreEqual(0, scenario.SentToAll<MapRoomCameraComponentState>().Count);
    }

    [TestMethod]
    public async Task LightIsAcceptedFromTheControllerOnly()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);

        // Simulating a camera is not controlling it.
        IReadOnlyList<Packet> fromSimulator = await scenario.LightAsync(scenario.PlayerA, new MapRoomCameraLight(cameraId, true));
        Assert.IsFalse(((MapRoomCameraLight)fromSimulator.Single()).Granted);
        Assert.IsFalse(scenario.CameraState(cameraId).LightOn);

        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB, cameraId, 0, true)).Granted);
        IReadOnlyList<Packet> fromController = await scenario.LightAsync(scenario.PlayerB, new MapRoomCameraLight(cameraId, true));

        MapRoomCameraLight accepted = (MapRoomCameraLight)fromController.Single();
        Assert.AreSame(accepted, scenario.SentToAll<MapRoomCameraLight>().Single());
        Assert.AreEqual((true, true, 1L), (accepted.On, accepted.Granted, accepted.Revision));
        Assert.IsTrue(scenario.CameraState(cameraId).LightOn);
    }

    [TestMethod]
    public async Task ClientCannotChangeACamerasNumberDockOrAnythingElseThroughItsMetadata()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        MapRoomCameraMetadata before = scenario.CameraState(cameraId);

        // From the player who simulates the camera, who is the one a battery report is accepted from.
        IReadOnlyList<Packet> sent = await scenario.UpdateMetadataAsync(scenario.PlayerA, cameraId, new MapRoomCameraMetadata(true, 9, 1f, 1f, 9, 77, 1));

        Assert.AreSame(before, scenario.CameraState(cameraId));
        Assert.AreEqual(cameraId, scenario.DockedCamera(0));
        Assert.IsNull(scenario.DockedCamera(1));
        Assert.AreEqual(0, sent.Count);
    }

    [TestMethod]
    public async Task CameraAClientMadeGetsTheNextNumberAndItsMakerIsToldWhichOne()
    {
        ScannerRoomScenarioFixture scenario = new();
        NitroxId craftedId = new("00000000-0000-0000-0000-000000000260");

        // A client cannot know which numbers are free; the one it would give by itself is the count of its cameras.
        IReadOnlyList<Packet> sent = await scenario.DropAsync(scenario.PlayerA, craftedId, new MapRoomCameraMetadata(true, 0, 5f, 5f, 0, 1));

        MapRoomCameraMetadata state = scenario.CameraState(craftedId);
        Assert.AreEqual(new MapRoomCameraMetadata(cameraNumber: 4).ToString(), state.ToString(), "The world has cameras 1 to 3, and a new camera is full, intact and dark.");
        EntityMetadataUpdate told = scenario.SentTo<EntityMetadataUpdate>(scenario.PlayerA).Single();
        Assert.AreEqual(craftedId, told.Id);
        Assert.AreSame(state, told.NewValue);
        CollectionAssert.Contains(sent.ToList(), told);
        Assert.IsTrue(scenario.Manager.IsScannerCamera(craftedId));
    }

    [TestMethod]
    public async Task NumberOfACameraThatIsGoneIsNotGivenToAnotherOne()
    {
        ScannerRoomScenarioFixture scenario = new();
        await scenario.DestroyAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraCId);

        await scenario.DropAsync(scenario.PlayerA, new NitroxId("00000000-0000-0000-0000-000000000260"));
        await scenario.PickupAsync(scenario.PlayerA, cameraId);
        await scenario.DropAsync(scenario.PlayerA, cameraId);

        Assert.AreEqual(4, scenario.CameraState(new NitroxId("00000000-0000-0000-0000-000000000260")).CameraNumber,
            "Every screen cycles through the numbers of all cameras; one that is reused would name two cameras over a session.");
        Assert.AreEqual(1, scenario.CameraState(cameraId).CameraNumber, "A camera keeps its number for as long as it exists.");
    }

    [TestMethod]
    public async Task CameraOutInTheWorldKeepsItsStateWhenTheRoomItCameFromIsDeconstructed()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, false)).Granted);
        scenario.SetCameraComponents(cameraId, 12f, 180f);
        MapRoomCameraMetadata before = scenario.CameraState(cameraId);
        Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(scenario.Room.Id).HasValue);

        await scenario.CleanupAsync(scenario.Room);

        Assert.AreSame(before, scenario.CameraState(cameraId));
        Assert.IsInstanceOfType<GlobalRootEntity>(Camera(scenario));
    }

    private static Entity Camera(ScannerRoomScenarioFixture scenario) => scenario.EntityRegistry.GetEntityById(cameraId).Value;
}
