using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Test.Server.Helper;
using Nitrox.Test.Server.Subnautica;
using Packet = Nitrox.Model.Packets.Packet;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.GameLogic;

/// <summary>
///     A camera keeps its light, battery, health and number on its own entity. Its entity changes shape with where the
///     camera is: a child of a room while docked, an entity of the world while loose, an inventory item while carried.
///     The state has to come through every one of these changes. Players change it with an entity metadata update, of
///     which only the part counts that the sender decides.
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
    [DataRow(false, false, false, 37f, 250f)]
    [DataRow(true, false, true, 37f, 250f)]
    [DataRow(false, true, false, 5f, 6f)]
    [DataRow(true, true, true, 5f, 6f)]
    public void OnlyWhatTheSenderDecidesIsTakenFromItsUpdate(bool maySetLight, bool maySetComponents, bool expectedLight, float expectedEnergy, float expectedHealth)
    {
        MapRoomCameraMetadata stored = new(false, 37f, 250f, 3, 1);
        MapRoomCameraMetadata requested = new(true, 5f, 6f, 77, MapRoomCameraMetadata.NOT_DOCKED);

        MapRoomCameraMetadata merged = ScannerRoomManager.Merge(stored, requested, maySetLight, maySetComponents);

        Assert.AreEqual(new MapRoomCameraMetadata(expectedLight, expectedEnergy, expectedHealth, 3, 1).ToString(), merged.ToString(),
            "The number and the dock are the server's, whoever sends the update.");
        Assert.AreEqual(!maySetLight && !maySetComponents, ReferenceEquals(stored, merged), "An update that changes nothing leaves the stored state as it is.");
    }

    [TestMethod]
    public async Task DockingAndUndockingChangeNothingOfACameraButItsDock()
    {
        ScannerRoomScenarioFixture scenario = new();
        scenario.SetCameraState(cameraId, new MapRoomCameraMetadata(true, 37f, 250f, 1));

        MapRoomCameraMetadata docked = (await scenario.DockAsync(scenario.PlayerA, cameraId, 1, true)).State;
        MapRoomCameraMetadata undocked = (await scenario.DockAsync(scenario.PlayerA, cameraId, 1, false)).State;

        Assert.AreEqual(new MapRoomCameraMetadata(true, 37f, 250f, 1, 1).ToString(), docked.ToString());
        Assert.AreEqual(new MapRoomCameraMetadata(true, 37f, 250f, 1).ToString(), undocked.ToString());
    }

    [TestMethod]
    public async Task LooseCameraTakesItsStateIntoAnInventoryAndBackIntoTheWorld()
    {
        ScannerRoomScenarioFixture scenario = new();
        scenario.SetCameraState(cameraId, new MapRoomCameraMetadata(false, 37f, 250f, 1));

        await scenario.PickupAsync(scenario.PlayerA, cameraId);

        Assert.IsInstanceOfType<InventoryItemEntity>(Camera(scenario));
        Assert.AreEqual(new MapRoomCameraMetadata(false, 37f, 250f, 1).ToString(), scenario.CameraState(cameraId).ToString());

        await scenario.DropAsync(scenario.PlayerA, cameraId);

        Assert.IsInstanceOfType<GlobalRootEntity>(Camera(scenario));
        Assert.AreEqual(new MapRoomCameraMetadata(false, 37f, 250f, 1).ToString(), scenario.CameraState(cameraId).ToString());
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted, "The camera that was dropped is a camera like any other.");
        Assert.AreEqual(new MapRoomCameraMetadata(false, 37f, 250f, 1, 0).ToString(), scenario.CameraState(cameraId).ToString());
    }

    [TestMethod]
    public async Task CameraPickedOutOfItsDockLeavesTheDockAndKeepsItsState()
    {
        ScannerRoomScenarioFixture scenario = new();
        scenario.SetCameraState(cameraId, new MapRoomCameraMetadata(false, 37f, 250f, 1));
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);

        // The game takes a docked camera out of its dock when it is picked up; the player's own report of that may come later.
        IReadOnlyList<Packet> sent = await scenario.PickupAsync(scenario.PlayerB, cameraId);
        ScannerRoomScenarioFixture.DockResult lateUndock = await scenario.DockAsync(scenario.PlayerB, cameraId, 0, false, establishAuthority: false);

        Assert.AreEqual(0, scenario.Room.ChildEntities.Count);
        Assert.IsInstanceOfType<InventoryItemEntity>(Camera(scenario));
        Assert.AreEqual(scenario.PlayerB.GameObjectId, Camera(scenario).ParentId);
        Assert.AreEqual(new MapRoomCameraMetadata(false, 37f, 250f, 1).ToString(), scenario.CameraState(cameraId).ToString());
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
        scenario.SetCameraState(cameraId, new MapRoomCameraMetadata(false, 37f, 250f, 1));
        MapRoomCameraMetadata forged = new(true, MapRoomCameraMetadata.MAX_ENERGY, MapRoomCameraMetadata.MAX_HEALTH, 77, 1);

        await scenario.PickupAsync(scenario.PlayerA, cameraId, forged);
        Assert.AreEqual(new MapRoomCameraMetadata(false, 37f, 250f, 1).ToString(), scenario.CameraState(cameraId).ToString());

        IReadOnlyList<Packet> sent = await scenario.DropAsync(scenario.PlayerA, cameraId, forged);
        Assert.AreEqual(new MapRoomCameraMetadata(false, 37f, 250f, 1).ToString(), scenario.CameraState(cameraId).ToString());
        Assert.AreEqual(0, scenario.Room.ChildEntities.Count, "A camera gets into a dock by a dock report only.");
        EntityMetadataUpdate told = sent.OfType<EntityMetadataUpdate>().Single();
        Assert.AreSame(scenario.CameraState(cameraId), told.NewValue, "The client that built the entity is told what the camera's state really is.");
    }

    [TestMethod]
    public async Task PickingUpALitCameraSwitchesItsLightOffAndItStaysOffWhereverTheCameraGoesNext()
    {
        // The game switches the light off on pickup (MapRoomCamera.OnPickedUp), in the game of the player who picks the
        // camera up. Everyone else learns it from the state the server keeps.
        ScannerRoomScenarioFixture scenario = new();
        scenario.SetCameraState(cameraId, new MapRoomCameraMetadata(true, 37f, 250f, 1));
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);

        await scenario.PickupAsync(scenario.PlayerA, cameraId);

        Assert.AreEqual(new MapRoomCameraMetadata(false, 37f, 250f, 1).ToString(), scenario.CameraState(cameraId).ToString(), "Nothing but the light changes.");
        Entity sentItem = scenario.SentToOthers<SpawnEntities>().Single().Entities.Single();
        Assert.IsFalse(((MapRoomCameraMetadata)sentItem.Metadata).LightOn, "The item the other players put into that player's inventory is a dark camera.");

        await scenario.DropAsync(scenario.PlayerA, cameraId, new MapRoomCameraMetadata(true));
        Assert.IsFalse(scenario.CameraState(cameraId).LightOn);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 1, true)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 1, false)).Granted);
        Assert.AreEqual(new MapRoomCameraMetadata(false, 37f, 250f, 1).ToString(), scenario.CameraState(cameraId).ToString());
    }

    [TestMethod]
    public async Task DockedCameraIsRechargedByThePlayerWhoSimulatesItWhoeverSimulatesTheRoom()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        Assert.IsTrue(scenario.Ownership.TryAcquire(scenario.Room.Id, scenario.PlayerB, SimulationLockType.TRANSIENT));
        scenario.SetCameraComponents(cameraId, 20f, 250f);
        MapRoomCameraMetadata before = scenario.CameraState(cameraId);

        IReadOnlyList<Packet> fromRoomSimulator = await scenario.ReportCameraAsync(scenario.PlayerB, cameraId, energy: 90f);

        Assert.AreSame(before, scenario.CameraState(cameraId));
        Assert.AreSame(before, ((EntityMetadataUpdate)fromRoomSimulator.Single()).NewValue, "The player who is refused is sent what the camera's battery really holds.");

        IReadOnlyList<Packet> fromCameraSimulator = await scenario.ReportCameraAsync(scenario.PlayerA, cameraId, energy: 30f);

        Assert.AreEqual(new MapRoomCameraMetadata(false, 30f, 250f, 1, 0).ToString(), scenario.CameraState(cameraId).ToString());
        Assert.AreEqual(0, fromCameraSimulator.Count, "Its simulating player is not sent back what it reported.");
    }

    [TestMethod]
    public async Task ControllerOfADockedCameraIsTheOneWhoReportsItsBatteryAndHealth()
    {
        // Taking control makes the controller the camera's simulating player. The player who simulated it before
        // must not go on recharging it against the controller's reports.
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB, cameraId, true)).Granted);

        await scenario.ReportCameraAsync(scenario.PlayerA, cameraId, energy: 90f);
        Assert.AreEqual(MapRoomCameraMetadata.MAX_ENERGY, scenario.CameraState(cameraId).Energy);
        await scenario.ReportCameraAsync(scenario.PlayerB, cameraId, energy: 80f);
        Assert.AreEqual(80f, scenario.CameraState(cameraId).Energy);

        // After the release the controller goes on simulating the camera, in its dock or out of it.
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB, cameraId, false)).Granted);
        await scenario.ReportCameraAsync(scenario.PlayerB, cameraId, energy: 85f);
        Assert.AreEqual(85f, scenario.CameraState(cameraId).Energy);
    }

    [TestMethod]
    public async Task BatteryOrHealthACameraCannotHaveIsRefusedAndTheSenderIsSentWhatIsStored()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        MapRoomCameraMetadata before = scenario.CameraState(cameraId);

        IReadOnlyList<Packet> sent = await scenario.ReportCameraAsync(scenario.PlayerA, cameraId, energy: 100.5f);

        Assert.AreSame(before, scenario.CameraState(cameraId));
        Assert.AreSame(before, ((EntityMetadataUpdate)sent.Single()).NewValue);
        Assert.AreSame(sent.Single(), scenario.SentTo<EntityMetadataUpdate>(scenario.PlayerA).Last());
    }

    [TestMethod]
    public async Task LightIsTakenFromThePlayerWhoHoldsTheCameraExclusivelyOnly()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);

        // Simulating a camera is not flying it.
        IReadOnlyList<Packet> fromSimulator = await scenario.ReportCameraAsync(scenario.PlayerA, cameraId, lightOn: true);
        Assert.IsFalse(scenario.CameraState(cameraId).LightOn);
        Assert.IsFalse(((MapRoomCameraMetadata)((EntityMetadataUpdate)fromSimulator.Single()).NewValue).LightOn, "Their camera shows a light nobody else sees, so they are corrected.");

        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB, cameraId, true)).Granted);
        IReadOnlyList<Packet> fromController = await scenario.ReportCameraAsync(scenario.PlayerB, cameraId, lightOn: true);

        Assert.IsTrue(scenario.CameraState(cameraId).LightOn);
        Assert.AreEqual(0, fromController.Count);
    }

    [TestMethod]
    public async Task ClientCannotChangeACamerasNumberOrDockThroughItsState()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        MapRoomCameraMetadata before = scenario.CameraState(cameraId);

        // From the player who simulates the camera, who decides more about it than anyone else.
        IReadOnlyList<Packet> sent = await scenario.UpdateMetadataAsync(scenario.PlayerA, cameraId, new MapRoomCameraMetadata(before.LightOn, 60f, before.Health, 77, 1));

        Assert.AreEqual(new MapRoomCameraMetadata(false, 60f, MapRoomCameraMetadata.MAX_HEALTH, 1, 0).ToString(), scenario.CameraState(cameraId).ToString());
        Assert.AreEqual(cameraId, scenario.DockedCamera(0));
        Assert.IsNull(scenario.DockedCamera(1));
        Assert.AreEqual(0, sent.Count);
    }

    [TestMethod]
    public async Task UpdateForACameraInAnInventoryChangesNothingAndIsAnsweredWithWhatIsStored()
    {
        ScannerRoomScenarioFixture scenario = new();
        scenario.SetCameraState(cameraId, new MapRoomCameraMetadata(false, 37f, 250f, 1));
        await scenario.PickupAsync(scenario.PlayerA, cameraId);
        MapRoomCameraMetadata carried = scenario.CameraState(cameraId);

        IReadOnlyList<Packet> sent = await scenario.ReportCameraAsync(scenario.PlayerA, cameraId, energy: 90f);

        Assert.AreSame(carried, scenario.CameraState(cameraId));
        Assert.AreSame(carried, ((EntityMetadataUpdate)sent.Single()).NewValue);
    }

    [TestMethod]
    public async Task AcceptedUpdateGoesToTheOtherPlayersAndNotBackToItsSender()
    {
        ConnectedPlayers players = new();
        ScannerRoomScenarioFixture scenario = new(connectedPlayers: players);
        ServerPlayer playerC = players.Connect("PlayerC");
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 1, true)).Granted);

        await scenario.UpdateMetadataAsync(scenario.PlayerA, cameraId, new MapRoomCameraMetadata(false, 61f, 320f, 0, MapRoomCameraMetadata.NOT_DOCKED));
        await scenario.UpdateMetadataAsync(scenario.PlayerA, cameraId, new MapRoomCameraMetadata(false, 61f, 320f, 0, MapRoomCameraMetadata.NOT_DOCKED));

        foreach (ServerPlayer other in new[] { scenario.PlayerB, playerC })
        {
            EntityMetadataUpdate relayed = scenario.SentTo<EntityMetadataUpdate>(other).Single();
            Assert.AreEqual(cameraId, relayed.Id);
            Assert.AreEqual(new MapRoomCameraMetadata(false, 61f, 320f, 1, 1).ToString(), relayed.NewValue.ToString(), "What is passed on is what the server stored: its number and dock.");
        }
        Assert.AreEqual(0, scenario.SentTo<EntityMetadataUpdate>(scenario.PlayerA).Count,
            "The camera's simulating player already holds this state. Sent back, it would undo what its game did to the battery in the meantime.");
    }

    [TestMethod]
    public async Task RefusedUpdateIsAnsweredWithWhatIsStoredAndNobodyElseIsTold()
    {
        ConnectedPlayers players = new();
        ScannerRoomScenarioFixture scenario = new(connectedPlayers: players);
        ServerPlayer playerC = players.Connect("PlayerC");
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 1, true)).Granted);
        MapRoomCameraMetadata stored = scenario.CameraState(cameraId);

        await scenario.UpdateMetadataAsync(scenario.PlayerB, cameraId, new MapRoomCameraMetadata(true, 5f, 5f, 9, 0));

        Assert.AreSame(stored, scenario.CameraState(cameraId));
        Assert.AreSame(stored, scenario.SentTo<EntityMetadataUpdate>(scenario.PlayerB).Single().NewValue, "Their game shows a state it made up; with this it shows the camera's own again.");
        Assert.AreEqual(0, scenario.SentTo<EntityMetadataUpdate>(scenario.PlayerA).Count + scenario.SentTo<EntityMetadataUpdate>(playerC).Count);
    }

    [TestMethod]
    public async Task UpdateThatIsOnlyPartlyTheSendersToDecideIsStoredInPartAndTheSenderIsToldToo()
    {
        ConnectedPlayers players = new();
        ScannerRoomScenarioFixture scenario = new(connectedPlayers: players);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 1, true)).Granted);

        // The battery is the simulating player's to report, the light is not.
        await scenario.UpdateMetadataAsync(scenario.PlayerA, cameraId, new MapRoomCameraMetadata(true, 61f, 320f, 1, 1));

        MapRoomCameraMetadata stored = scenario.CameraState(cameraId);
        Assert.AreEqual(new MapRoomCameraMetadata(false, 61f, 320f, 1, 1).ToString(), stored.ToString());
        Assert.AreSame(stored, scenario.SentTo<EntityMetadataUpdate>(scenario.PlayerA).Single().NewValue);
        Assert.AreSame(stored, scenario.SentTo<EntityMetadataUpdate>(scenario.PlayerB).Single().NewValue);
    }

    [TestMethod]
    [DataRow(1425)]
    [DataRow(2576)]
    public async Task UpdatesOfTwoPlayersWhileTheCameraChangesHandsLeaveTheStateOfOneOfThem(int seed)
    {
        Random random = new(seed);
        (bool, float, float) initial = (false, MapRoomCameraMetadata.MAX_ENERGY, MapRoomCameraMetadata.MAX_HEALTH);

        for (int iteration = 0; iteration < 24; iteration++)
        {
            ScannerRoomScenarioFixture scenario = new();
            Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 1, true)).Granted);
            Func<Task>[] steps =
            [
                () => scenario.UpdateMetadataAsync(scenario.PlayerA, cameraId, new MapRoomCameraMetadata(false, 10f, 110f, 71, 0)),
                () => scenario.UpdateMetadataAsync(scenario.PlayerB, cameraId, new MapRoomCameraMetadata(true, 20f, 220f, 72, MapRoomCameraMetadata.NOT_DOCKED)),
                () =>
                {
                    // The camera goes from the player who simulates it to one who holds it exclusively.
                    scenario.Ownership.ForceAcquire(cameraId, scenario.PlayerB, SimulationLockType.EXCLUSIVE);
                    return Task.CompletedTask;
                }
            ];
            random.Shuffle(steps);

            using Barrier start = new(steps.Length);
            await Task.WhenAll(steps.Select(step => Task.Run(async () =>
            {
                start.SignalAndWait();
                await step();
            })));

            MapRoomCameraMetadata state = scenario.CameraState(cameraId);
            (bool, float, float)[] wholeStates = [initial, (false, 10f, 110f), (true, 20f, 220f)];
            CollectionAssert.Contains(wholeStates, (state.LightOn, state.Energy, state.Health),
                $"Light, battery and health must be the ones one player sent, not a mix of both. seed={seed}, iteration={iteration}");
            Assert.AreEqual((1, 1), (state.CameraNumber, state.DockingIndex), $"The number and the dock are never a client's. seed={seed}, iteration={iteration}");
            scenario.AssertCanonicalInvariants();
        }
    }

    [TestMethod]
    [DataRow(1533)]
    [DataRow(2410)]
    public async Task DockReportedWhileTheCamerasStateIsUpdatedIsNotLost(int seed)
    {
        Random random = new(seed);

        for (int iteration = 0; iteration < 24; iteration++)
        {
            ScannerRoomScenarioFixture scenario = new();
            Assert.IsTrue(scenario.Ownership.TryAcquire(cameraId, scenario.PlayerA, SimulationLockType.TRANSIENT));
            // What the client knew of the camera when it made the update: not docked yet.
            MapRoomCameraMetadata update = new(false, 55f, 333f, 1, MapRoomCameraMetadata.NOT_DOCKED);
            Func<Task>[] steps =
            [
                () => scenario.DockAsync(scenario.PlayerA, cameraId, 0, true, establishAuthority: false),
                () => scenario.UpdateMetadataAsync(scenario.PlayerA, cameraId, update)
            ];
            random.Shuffle(steps);

            using Barrier start = new(steps.Length);
            await Task.WhenAll(steps.Select(step => Task.Run(async () =>
            {
                start.SignalAndWait();
                await step();
            })));

            Assert.AreEqual(new MapRoomCameraMetadata(false, 55f, 333f, 1, 0).ToString(), scenario.CameraState(cameraId).ToString(), $"seed={seed}, iteration={iteration}");
            Assert.AreEqual(cameraId, scenario.DockedCamera(0), $"seed={seed}, iteration={iteration}");
            scenario.AssertCanonicalInvariants();
        }
    }

    [TestMethod]
    public async Task CameraAClientMadeGetsTheNextNumberAndItsMakerIsToldWhichOne()
    {
        ScannerRoomScenarioFixture scenario = new();
        NitroxId craftedId = new("00000000-0000-0000-0000-000000000260");

        // A client cannot know which numbers are free; the one it would give by itself is the count of its cameras.
        IReadOnlyList<Packet> sent = await scenario.DropAsync(scenario.PlayerA, craftedId, new MapRoomCameraMetadata(true, 5f, 5f, 1));

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

    [TestMethod]
    public async Task CameraInAnInventoryKeepsItsStateWhenTheRoomItWasDockedInIsDeconstructedAndIsACameraLikeAnyOtherOnceDropped()
    {
        ScannerRoomScenarioFixture scenario = new();
        MapRoomEntity otherRoom = scenario.AddRoom(new MapRoomEntity(new NitroxId("00000000-0000-0000-0000-000000000401"), new NitroxId("00000000-0000-0000-0000-000000000400"), new NitroxInt3(5, -2, 9)));
        scenario.SetCameraState(cameraId, new MapRoomCameraMetadata(false, 37f, 250f, 1));
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        await scenario.PickupAsync(scenario.PlayerB, cameraId);
        Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(scenario.Room.Id).HasValue);

        IReadOnlyList<Packet> sent = await scenario.CleanupAsync(scenario.Room);

        Assert.AreEqual(0, sent.OfType<EntityDestroyed>().Count(), "Only the cameras that sit in the room's docks go with it.");
        Assert.IsInstanceOfType<InventoryItemEntity>(Camera(scenario));
        Assert.AreEqual(new MapRoomCameraMetadata(false, 37f, 250f, 1).ToString(), scenario.CameraState(cameraId).ToString());

        await scenario.DropAsync(scenario.PlayerB, cameraId);

        Assert.IsInstanceOfType<GlobalRootEntity>(Camera(scenario));
        Assert.AreEqual(new MapRoomCameraMetadata(false, 37f, 250f, 1).ToString(), scenario.CameraState(cameraId).ToString());
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted, "It can be flown,");
        Assert.AreEqual(0, (await scenario.ReportCameraAsync(scenario.PlayerA, cameraId, lightOn: true, energy: 30f)).Count, "its controller sets its light and reports its battery,");
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 1, true, establishAuthority: false, roomId: otherRoom.Id)).Granted, "and it goes into a dock of another room.");
        Assert.AreEqual(new MapRoomCameraMetadata(true, 30f, 250f, 1, 1).ToString(), scenario.CameraState(cameraId).ToString());
    }

    private static Entity Camera(ScannerRoomScenarioFixture scenario) => scenario.EntityRegistry.GetEntityById(cameraId).Value;
}
