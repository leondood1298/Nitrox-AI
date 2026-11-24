using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.GameLogic;
using Nitrox.Test.Server.Helper;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     The gate every entity metadata update passes on the server. Covered here: the arms for base power, whose state
///     only the player simulating the source decides, and the arms for Scanner Rooms, whose scan target any player who
///     sees the room may choose while scan progress belongs to the room's simulating player. A room's fabricator is a
///     child entity whose state any player may set, like any other fabricator's. A Lifepod's repair state can only be
///     merged with another repair state, never replaced. A medical cabinet's state comes from any player and is only kept
///     for a cabinet, and so is the text of a rocket's time capsule for the capsule. A water filtration machine's timers
///     come from its simulating player only and are not replaced by another kind of metadata. The first answer about a
///     time capsule in the world is kept for everybody.
/// </summary>
[TestClass]
public sealed class EntityMetadataUpdateProcessorTest
{
    private static readonly NitroxTechType quartz = new("Quartz");
    private static readonly NitroxTechType titanium = new("Titanium");

    private ConnectedPlayers players;
    private EntityRegistry entityRegistry;
    private SimulationOwnershipData simulationOwnershipData;
    private BasePowerDiagnostics diagnostics;
    private ScannerRoomDiagnostics scannerDiagnostics;
    private EntityMetadataUpdateProcessor processor;
    private IPacketSender packetSender;
    private Player simulator;
    private Player otherPlayer;
    private Player thirdPlayer;

    [TestInitialize]
    public void Init()
    {
        players = new ConnectedPlayers();
        entityRegistry = new EntityRegistry(NullLogger<EntityRegistry>.Instance);
        simulationOwnershipData = new SimulationOwnershipData();
        diagnostics = new BasePowerDiagnostics();
        BasePowerManager basePowerManager = new(entityRegistry, simulationOwnershipData, diagnostics, NullLogger<BasePowerManager>.Instance);
        packetSender = Substitute.For<IPacketSender>();
        scannerDiagnostics = new ScannerRoomDiagnostics(NullLogger<ScannerRoomDiagnostics>.Instance);
        ScannerRoomManager scannerRoomManager = ScannerRoomScenarioFixture.CreateManager(entityRegistry, simulationOwnershipData, scannerDiagnostics, packetSender, players.PlayerManager);
        WorldEntityManager worldEntityManager = new(packetSender, entityRegistry, null!, players.PlayerManager, null!, NullLogger<WorldEntityManager>.Instance);
        EntitySimulation entitySimulation = new(packetSender, entityRegistry, worldEntityManager, simulationOwnershipData, players.PlayerManager, scannerRoomManager,
            NullLogger<EntitySimulation>.Instance);
        processor = new EntityMetadataUpdateProcessor(players.PlayerManager, entityRegistry, simulationOwnershipData, scannerRoomManager, basePowerManager,
            worldEntityManager, entitySimulation, new ItemClaims(entityRegistry, players.PlayerManager), NullLogger<EntityMetadataUpdateProcessor>.Instance);
        simulator = players.Connect("Simulator");
        otherPlayer = players.Connect("Other");
        thirdPlayer = players.Connect("Third");
    }

    [TestMethod]
    public async Task StateFromTheSimulatingPlayerIsStoredAndSentToTheOthers()
    {
        ModuleEntity panel = AddSource(new PowerSourceMetadata(30f, 0f, BasePowerSourceType.SOLAR), simulator);

        await Send(simulator, panel, new PowerSourceMetadata(40f, 0f, BasePowerSourceType.SOLAR));

        Assert.AreEqual(40f, ((PowerSourceMetadata)panel.Metadata).Power);
        foreach (Player receiver in new[] { otherPlayer, thirdPlayer })
        {
            EntityMetadataUpdate relayed = ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, receiver).Single();
            Assert.AreEqual(panel.Id, relayed.Id);
            Assert.AreEqual(40f, ((PowerSourceMetadata)relayed.NewValue).Power);
        }
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, simulator).Count,
            "The simulating player already holds this state; getting it back would undo what it simulated meanwhile.");
    }

    [TestMethod]
    public async Task StateFromAPlayerWhoDoesNotSimulateTheSourceIsAnsweredWithTheStoredState()
    {
        ModuleEntity panel = AddSource(new PowerSourceMetadata(30f, 0f, BasePowerSourceType.SOLAR), simulator);

        await Send(otherPlayer, panel, new PowerSourceMetadata(70f, 0f, BasePowerSourceType.SOLAR));

        Assert.AreEqual(30f, ((PowerSourceMetadata)panel.Metadata).Power);
        EntityMetadataUpdate correction = ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, otherPlayer).Single();
        Assert.AreEqual(30f, ((PowerSourceMetadata)correction.NewValue).Power);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, simulator).Count);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, thirdPlayer).Count);
    }

    [TestMethod]
    public async Task LateStateFromThePreviousSimulatingPlayerCannotReplaceTheNewOnes()
    {
        ModuleEntity panel = AddSource(new PowerSourceMetadata(30f, 0f, BasePowerSourceType.SOLAR), simulator);
        simulationOwnershipData.RevokeIfOwner(panel.Id, simulator);
        Assert.IsTrue(simulationOwnershipData.TryAcquire(panel.Id, otherPlayer, SimulationLockType.TRANSIENT));
        await Send(otherPlayer, panel, new PowerSourceMetadata(45f, 0f, BasePowerSourceType.SOLAR));
        packetSender.ClearReceivedCalls();

        await Send(simulator, panel, new PowerSourceMetadata(31f, 0f, BasePowerSourceType.SOLAR));

        Assert.AreEqual(45f, ((PowerSourceMetadata)panel.Metadata).Power);
        Assert.AreEqual(45f, ((PowerSourceMetadata)ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, simulator).Single().NewValue).Power,
            "It is told the level that counts now.");
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, thirdPlayer).Count);
    }

    [TestMethod]
    public async Task StateASourceCannotHaveIsNotStoredAndTheSenderIsCorrected()
    {
        ModuleEntity panel = AddSource(new PowerSourceMetadata(30f, 0f, BasePowerSourceType.SOLAR), simulator);

        await Send(simulator, panel, new PowerSourceMetadata(9000f, 0f, BasePowerSourceType.SOLAR));

        Assert.AreEqual(30f, ((PowerSourceMetadata)panel.Metadata).Power);
        Assert.AreEqual(30f, ((PowerSourceMetadata)ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, simulator).Single().NewValue).Power);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, otherPlayer).Count);
    }

    [TestMethod]
    public async Task OtherMetadataCannotTakeThePlaceOfASourcesPowerState()
    {
        // It would erase level and fuel progress; the source would then come back full for the next player who joins.
        ModuleEntity panel = AddSource(new PowerSourceMetadata(30f, 0f, BasePowerSourceType.SOLAR), simulator);

        await Send(otherPlayer, panel, new BatteryMetadata(100f));
        await Send(simulator, panel, new BatteryMetadata(100f));

        Assert.AreEqual(30f, ((PowerSourceMetadata)panel.Metadata).Power);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task TemperatureFromAnyPlayerIsMergedIntoThePowerStateAndPassedOn()
    {
        // Every player near a thermal plant measures the water; none of them needs to simulate the plant for that.
        ModuleEntity plant = AddSource(new PowerSourceMetadata(120f, 0f, BasePowerSourceType.THERMAL, 40f), simulator);

        await Send(otherPlayer, plant, new ThermalPlantMetadata(63f));

        PowerSourceMetadata stored = (PowerSourceMetadata)plant.Metadata;
        Assert.AreEqual(63f, stored.Temperature);
        Assert.AreEqual(120f, stored.Power);
        Assert.AreEqual(63f, ((ThermalPlantMetadata)ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, simulator).Single().NewValue).Temperature);
        Assert.AreEqual(63f, ((ThermalPlantMetadata)ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, thirdPlayer).Single().NewValue).Temperature);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, otherPlayer).Count);
    }

    [TestMethod]
    public async Task TemperatureThatIsNoNewsIsNotPassedOn()
    {
        ModuleEntity plant = AddSource(new PowerSourceMetadata(120f, 0f, BasePowerSourceType.THERMAL, 40f), simulator);

        await Send(otherPlayer, plant, new ThermalPlantMetadata(40f));
        await Send(otherPlayer, plant, new ThermalPlantMetadata(float.NaN));

        Assert.AreEqual(40f, ((PowerSourceMetadata)plant.Metadata).Temperature);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task ImpossibleTemperatureIsNotStoredOrPassedOnAsReported()
    {
        ModuleEntity plant = AddSource(new PowerSourceMetadata(120f, 0f, BasePowerSourceType.THERMAL, 40f), simulator);
        ModuleEntity newPlant = AddSource(null, simulator);
        newPlant.TechType = new NitroxTechType("ThermalPlant");

        await Send(otherPlayer, plant, new ThermalPlantMetadata(1e9f));
        await Send(otherPlayer, newPlant, new ThermalPlantMetadata(1e9f));

        Assert.AreEqual(BasePowerManager.MAX_THERMAL_TEMPERATURE, ((PowerSourceMetadata)plant.Metadata).Temperature);
        Assert.AreEqual(BasePowerManager.MAX_THERMAL_TEMPERATURE, ((ThermalPlantMetadata)newPlant.Metadata).Temperature,
            "A plant that has no power state yet stores the report itself.");
        foreach (EntityMetadataUpdate relayed in ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, thirdPlayer))
        {
            Assert.AreEqual(BasePowerManager.MAX_THERMAL_TEMPERATURE, ((ThermalPlantMetadata)relayed.NewValue).Temperature);
        }
        Assert.AreEqual(2, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, thirdPlayer).Count);
    }

    [TestMethod]
    public async Task TemperatureSentForSomethingThatIsNoThermalPlantChangesNothing()
    {
        ModuleEntity locker = AddSource(null, simulator);
        locker.TechType = new NitroxTechType("Locker");

        await Send(otherPlayer, locker, new ThermalPlantMetadata(50f));

        Assert.IsNull(locker.Metadata);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task StateForASourceTheServerDoesNotHaveIsCountedAndNobodyIsTold()
    {
        // The source was destroyed while the state travelled, or it only exists on that client.
        await processor.Process(new AuthProcessorContext(simulator, packetSender),
            new EntityMetadataUpdate(new NitroxId(), new PowerSourceMetadata(40f, 0f, BasePowerSourceType.SOLAR)));

        Assert.AreEqual(1L, diagnostics.RejectedUpdates);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task MetadataThatHasNothingToDoWithPowerStillPassesFromAnyPlayer()
    {
        ModuleEntity locker = AddSource(null, simulator);

        await Send(otherPlayer, locker, new BatteryMetadata(55f));

        Assert.AreEqual(55f, ((BatteryMetadata)locker.Metadata).Charge);
        Assert.AreEqual(1, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, simulator).Count);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, otherPlayer).Count);
    }

    [TestMethod]
    public async Task ScanTargetFromAnyPlayerIsStoredAndSentToEveryoneWithTheSender()
    {
        // The server sets the generation, which the sender does not have yet.
        MapRoomEntity room = AddRoom(new MapRoomMetadata(quartz, 4, 2), simulator);

        await Send(otherPlayer, room, new MapRoomMetadata(titanium, 0, 2));

        MapRoomMetadata stored = (MapRoomMetadata)room.Metadata;
        Assert.AreEqual(titanium, stored.TypeToScan);
        Assert.AreEqual(0, stored.NumNodesScanned);
        Assert.AreEqual(3, stored.Generation);
        foreach (Player receiver in new[] { simulator, otherPlayer, thirdPlayer })
        {
            MapRoomMetadata sent = (MapRoomMetadata)ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, receiver).Single().NewValue;
            Assert.AreEqual(titanium, sent.TypeToScan);
            Assert.AreEqual(3, sent.Generation);
        }
    }

    [TestMethod]
    public async Task StoppingAScanIsATargetLikeAnyOther()
    {
        MapRoomEntity room = AddRoom(new MapRoomMetadata(quartz, 4, 2), simulator);

        await Send(thirdPlayer, room, new MapRoomMetadata(NitroxTechType.None, 4, 2));

        MapRoomMetadata stored = (MapRoomMetadata)room.Metadata;
        Assert.AreEqual(NitroxTechType.None, stored.TypeToScan);
        Assert.AreEqual(0, stored.NumNodesScanned);
        Assert.AreEqual(3, stored.Generation);
        Assert.AreEqual(3, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task ScanProgressFromTheSimulatingPlayerIsStoredAndSentToTheOthers()
    {
        // The simulating player's scan has gone on since it sent this. Its own number sent back would set it back.
        MapRoomEntity room = AddRoom(new MapRoomMetadata(quartz, 4, 2), simulator);

        await Send(simulator, room, new MapRoomMetadata(quartz, 5, 2));

        MapRoomMetadata stored = (MapRoomMetadata)room.Metadata;
        Assert.AreEqual(5, stored.NumNodesScanned);
        Assert.AreEqual(2, stored.Generation);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, simulator).Count);
        Assert.AreEqual(5, ((MapRoomMetadata)ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, otherPlayer).Single().NewValue).NumNodesScanned);
        Assert.AreEqual(2, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task ScanProgressFromAPlayerWhoDoesNotSimulateTheRoomIsAnsweredWithTheStoredState()
    {
        MapRoomEntity room = AddRoom(new MapRoomMetadata(quartz, 4, 2), simulator);

        await Send(otherPlayer, room, new MapRoomMetadata(quartz, 9, 2));

        Assert.AreEqual(4, ((MapRoomMetadata)room.Metadata).NumNodesScanned);
        Assert.AreEqual(4, ((MapRoomMetadata)ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, otherPlayer).Single().NewValue).NumNodesScanned,
            "Its own screen shows the count it sent until it is given the real one.");
        Assert.AreEqual(1, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task ScanTargetChosenAgainstAnOlderTargetIsAnsweredWithTheStoredState()
    {
        MapRoomEntity room = AddRoom(new MapRoomMetadata(quartz, 4, 2), simulator);

        await Send(otherPlayer, room, new MapRoomMetadata(titanium, 0, 1));

        Assert.AreEqual(quartz, ((MapRoomMetadata)room.Metadata).TypeToScan);
        MapRoomMetadata correction = (MapRoomMetadata)ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, otherPlayer).Single().NewValue;
        Assert.AreEqual(quartz, correction.TypeToScan);
        Assert.AreEqual(4, correction.NumNodesScanned);
        Assert.AreEqual(2, correction.Generation, "With the generation it is given, its next request can be accepted.");
        Assert.AreEqual(1, packetSender.ReceivedCalls().Count(), "Nobody else is told: for them nothing changed.");
    }

    [TestMethod]
    public async Task RefusedRequestCanBeRepeatedWithTheStateFromTheAnswer()
    {
        // A client applies its own scan target before the server has seen it. Nobody else's update tells it what the
        // room's state really is, and with a stale generation all its later requests would be refused as well.
        MapRoomEntity room = AddRoom(new MapRoomMetadata(quartz, 4, 2), simulator);
        await Send(otherPlayer, room, new MapRoomMetadata(titanium, 0, 1));
        MapRoomMetadata answer = (MapRoomMetadata)ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, otherPlayer).Single().NewValue;

        await Send(otherPlayer, room, new MapRoomMetadata(titanium, 0, answer.Generation));

        Assert.AreEqual(titanium, ((MapRoomMetadata)room.Metadata).TypeToScan);
    }

    [TestMethod]
    public async Task RoomThatNeverScannedAnswersWithAnIdleState()
    {
        MapRoomEntity room = AddRoom(null, simulator);

        await Send(otherPlayer, room, new MapRoomMetadata(titanium, 0, 5));

        MapRoomMetadata answer = (MapRoomMetadata)ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, otherPlayer).Single().NewValue;
        Assert.AreEqual(NitroxTechType.None, answer.TypeToScan);
        Assert.AreEqual(0, answer.Generation);
        Assert.IsNull(room.Metadata, "The refused request changed nothing.");
    }

    [TestMethod]
    public async Task ScanProgressThatIsNoNewsIsAnsweredWithTheStoredState()
    {
        MapRoomEntity room = AddRoom(new MapRoomMetadata(quartz, 4, 2), simulator);

        await Send(simulator, room, new MapRoomMetadata(quartz, 3, 2));

        Assert.AreEqual(4, ((MapRoomMetadata)room.Metadata).NumNodesScanned);
        Assert.AreEqual(4, ((MapRoomMetadata)ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, simulator).Single().NewValue).NumNodesScanned);
        Assert.AreEqual(1, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task ScanStateForSomethingThatIsNoScannerRoomIsNotStoredAndNobodyIsTold()
    {
        ModuleEntity locker = AddSource(null, simulator);

        await Send(simulator, locker, new MapRoomMetadata(quartz, 0));

        Assert.IsNull(locker.Metadata);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task FabricatorStateIsStoredOnTheRoomsFabricatorAndSentToTheOthers()
    {
        // The fabricator is a child entity of the room, and what it crafts is its metadata like for any fabricator.
        MapRoomEntity room = AddRoom(new MapRoomMetadata(quartz, 4, 2), simulator);
        PathBasedChildEntity fabricator = new("fabricator", new NitroxId(), null, null, room.Id, []);
        room.ChildEntities.Add(fabricator);
        entityRegistry.AddEntity(fabricator);
        CrafterMetadata crafting = new(new NitroxTechType("MapRoomHUDChip"), 12.5f, 4f, 1, 0);

        await Send(otherPlayer, fabricator, crafting);

        Assert.AreSame(crafting, fabricator.Metadata);
        Assert.AreEqual(quartz, ((MapRoomMetadata)room.Metadata).TypeToScan, "The room's scan state is another thing and stays.");
        Assert.AreSame(crafting, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, simulator).Single().NewValue);
        Assert.AreSame(crafting, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, thirdPlayer).Single().NewValue);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, otherPlayer).Count);
    }

    [TestMethod]
    public async Task ASecondCraftOnABusyFabricatorIsAnsweredWithTheRunningOne()
    {
        PathBasedChildEntity fabricator = new("fabricator", new NitroxId(), null, null, null, []);
        entityRegistry.AddEntity(fabricator);
        CrafterMetadata first = new(titanium, 12.5f, 4f, 1, -1);
        await Send(simulator, fabricator, first);
        packetSender.ClearReceivedCalls();

        await Send(otherPlayer, fabricator, new CrafterMetadata(quartz, 13f, 4f, 1, -1));

        Assert.AreSame(first, fabricator.Metadata);
        Assert.AreSame(first, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, otherPlayer).Single().NewValue);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, thirdPlayer).Count);
    }

    [TestMethod]
    public async Task FabricatorStateSentForTheRoomItselfIsRefusedAndTheRoomKeepsItsScanState()
    {
        // What a client would send if it took the room's object for its fabricator.
        MapRoomEntity room = AddRoom(new MapRoomMetadata(quartz, 4, 2), simulator);
        MapRoomMetadata scanState = (MapRoomMetadata)room.Metadata;

        await Send(simulator, room, new CrafterMetadata(new NitroxTechType("MapRoomHUDChip"), 12.5f, 4f, 1, 0));
        await Send(simulator, room, new CrafterMetadata(new NitroxTechType("MapRoomHUDChip"), 20f, 4f, 1, 0));

        Assert.AreSame(scanState, room.Metadata, "Stored on the room, the crafting state would replace what the room scans for.");
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
        ScannerRoomDiagnosticEntry recorded = scannerDiagnostics.GetHistory().Single();
        Assert.AreEqual(("fabricator", ScannerRoomDiagnosticOutcome.REJECTED, "sent_for_room"), (recorded.EventName, recorded.Outcome, recorded.Reason), "Written once, however often it is sent.");
    }

    [TestMethod]
    public async Task OtherMetadataCannotTakeThePlaceOfWhatARoomScansFor()
    {
        // An entity has one metadata slot. With something else in it the room would scan for nothing for the next
        // player who joins and after a restart.
        MapRoomEntity room = AddRoom(new MapRoomMetadata(quartz, 4, 2), simulator);
        MapRoomMetadata scanState = (MapRoomMetadata)room.Metadata;
        MapRoomEntity newRoom = AddRoom(null, simulator);

        foreach (Player sender in new[] { simulator, otherPlayer })
        {
            await Send(sender, room, new BatteryMetadata(100f));
            await Send(sender, room, new PowerSourceMetadata(40f, 0f, BasePowerSourceType.SOLAR));
            await Send(sender, room, new ThermalPlantMetadata(60f));
            await Send(sender, newRoom, new BatteryMetadata(100f));
        }

        Assert.AreSame(scanState, room.Metadata);
        Assert.IsNull(newRoom.Metadata, "A room that never scanned is a room all the same.");
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
        Assert.IsTrue(scannerDiagnostics.GetHistory().Any(entry => entry is { EventName: "metadata", Outcome: ScannerRoomDiagnosticOutcome.REJECTED, Reason: "replaced_by_batterymetadata" }));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task OtherMetadataCannotTakeThePlaceOfACamerasState(bool docked)
    {
        // The dock, the number, the light, the battery and the health of a camera are one piece of metadata, in a
        // dock as out in the world.
        MapRoomEntity room = AddRoom(new MapRoomMetadata(quartz, 4, 2), simulator);
        MapRoomCameraMetadata cameraState = new(true, 60f, 80f, 3, docked ? 1 : MapRoomCameraMetadata.NOT_DOCKED);
        GlobalRootEntity camera = AddCamera(cameraState, docked ? room : null, simulator);
        Assert.IsTrue(simulationOwnershipData.TryAcquire(camera.Id, simulator, SimulationLockType.EXCLUSIVE), "Not even the player who controls it.");

        foreach (Player sender in new[] { simulator, otherPlayer })
        {
            await Send(sender, camera, new BatteryMetadata(100f));
            await Send(sender, camera, new MapRoomMetadata(titanium, 0));
            await Send(sender, camera, new CrafterMetadata(new NitroxTechType("MapRoomHUDChip"), 12.5f, 4f, 1, 0));
            await Send(sender, camera, new PowerSourceMetadata(40f, 0f, BasePowerSourceType.SOLAR));
        }

        Assert.AreSame(cameraState, camera.Metadata);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task OtherMetadataCannotTakeThePlaceOfTheStateOfACameraThatHoldsNoneYetOrLiesInAnInventory()
    {
        GlobalRootEntity newCamera = AddCamera(null, null, simulator);
        InventoryItemEntity carried = new(new NitroxId(), "camera-class-id", new NitroxTechType("MapRoomCamera"), new MapRoomCameraMetadata(false, 60f, 80f, 3), simulator.GameObjectId, []);
        EntityMetadata carriedState = carried.Metadata;
        entityRegistry.AddEntity(carried);

        await Send(otherPlayer, newCamera, new BatteryMetadata(100f));
        await Send(simulator, carried, new BatteryMetadata(100f));

        Assert.IsNull(newCamera.Metadata, "Known as a camera by what it is.");
        Assert.AreSame(carriedState, carried.Metadata);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task CameraStateForSomethingThatIsNoCameraIsNotStoredAndNobodyIsTold()
    {
        ModuleEntity locker = AddSource(null, simulator);

        await Send(simulator, locker, new MapRoomCameraMetadata(true, 1f, 1f, 9, 1));

        Assert.IsNull(locker.Metadata);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task CameraStateForACameraTheServerNoLongerHasIsDroppedQuietly()
    {
        // Its simulating player can report its battery once more while the camera's removal is on its way to them.
        await processor.Process(new AuthProcessorContext(simulator, packetSender), new EntityMetadataUpdate(new NitroxId(), new MapRoomCameraMetadata(false, 50f)));

        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task OtherMetadataCannotTakeThePlaceOfAPodsRepairState()
    {
        // With something else in its one metadata slot, the pod and its radio would come back broken for the next
        // player who joins and after a restart.
        EscapePodMetadata repaired = new(true, true);
        EscapePodEntity pod = new(NitroxVector3.Zero, new NitroxId(), repaired);
        entityRegistry.AddEntity(pod);

        foreach (Player sender in new[] { simulator, otherPlayer })
        {
            await Send(sender, pod, new BatteryMetadata(100f));
            await Send(sender, pod, new CrafterMetadata(new NitroxTechType("FirstAidKit"), 1f, 2f, 1, 0));
            await Send(sender, pod, new PowerSourceMetadata(40f, 0f, BasePowerSourceType.SOLAR));
            await Send(sender, pod, new MapRoomMetadata(quartz, 0));
        }

        Assert.AreSame(repaired, pod.Metadata);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task APodsRepairStateIsMergedAndSentToTheOthers()
    {
        EscapePodEntity pod = new(NitroxVector3.Zero, new NitroxId(), new EscapePodMetadata(true, false));
        entityRegistry.AddEntity(pod);

        await Send(otherPlayer, pod, new EscapePodMetadata(false, true));

        EscapePodMetadata stored = (EscapePodMetadata)pod.Metadata;
        Assert.IsTrue(stored.PodRepaired, "A repair is never undone.");
        Assert.IsTrue(stored.RadioRepaired);
        Assert.IsTrue(((EscapePodMetadata)ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, simulator).Single().NewValue).PodRepaired);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, otherPlayer).Count);
    }

    [TestMethod]
    public async Task ALifepodCellsLevelFromItsSimulatingPlayerIsStoredAndNothingElseTakesItsPlace()
    {
        EscapePodPowerCellEntity cell = new(new NitroxId(), 0, new NitroxId());
        entityRegistry.AddEntity(cell);
        Assert.IsTrue(simulationOwnershipData.TryAcquire(cell.Id, simulator, SimulationLockType.TRANSIENT));

        // Before its first report the cell holds no metadata; it must not take another kind then either
        await Send(otherPlayer, cell, new BatteryMetadata(100f));
        Assert.IsNull(cell.Metadata);

        await Send(simulator, cell, new PowerSourceMetadata(18f, 0f, BasePowerSourceType.ESCAPE_POD));
        await Send(otherPlayer, cell, new PowerSourceMetadata(25f, 0f, BasePowerSourceType.ESCAPE_POD));

        Assert.AreEqual(18f, ((PowerSourceMetadata)cell.Metadata).Power);
        Assert.AreEqual(18f, ((PowerSourceMetadata)ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, thirdPlayer).Single().NewValue).Power);
        Assert.AreEqual(18f, ((PowerSourceMetadata)ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, otherPlayer).Last().NewValue).Power,
            "The other player is given the stored level back.");
    }

    [TestMethod]
    public async Task CabinetStateFromAnyPlayerIsStoredForThePodsCabinetAndABuiltOne()
    {
        // The Lifepod's cabinet is a child of the pod; one built in a base is a module of it.
        EscapePodEntity pod = new(NitroxVector3.Zero, new NitroxId(), new EscapePodMetadata(false, false));
        PrefabChildEntity podCabinet = new(new NitroxId(), "c0175cf7-0b6a-4a1d-938f-dad0dbb6fa06", new NitroxTechType("MedicalCabinet"), 0, null, pod.Id);
        pod.ChildEntities.Add(podCabinet);
        entityRegistry.AddOrUpdate(pod);
        ModuleEntity builtCabinet = AddSource(null, simulator);
        builtCabinet.TechType = new NitroxTechType("MedicalCabinet");
        MedicalCabinetMetadata taken = new(false, false, 1200f);
        MedicalCabinetMetadata opened = new(true, true, 900f);

        await Send(otherPlayer, podCabinet, taken);
        await Send(thirdPlayer, builtCabinet, opened);

        Assert.AreSame(taken, podCabinet.Metadata);
        Assert.AreSame(opened, builtCabinet.Metadata);
        Assert.IsInstanceOfType<EscapePodMetadata>(pod.Metadata, "The cabinet's state is its own, not the pod's.");
        Assert.AreSame(taken, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, simulator).First().NewValue);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, otherPlayer).Count(update => update.Id.Equals(podCabinet.Id)));
    }

    [TestMethod]
    public async Task CabinetStateForSomethingThatIsNoCabinetIsNotStored()
    {
        ModuleEntity locker = AddSource(null, simulator);
        locker.TechType = new NitroxTechType("Locker");

        await Send(otherPlayer, locker, new MedicalCabinetMetadata(true, true, 900f));

        Assert.IsNull(locker.Metadata);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task TimeCapsuleTextFromAnyPlayerIsStoredForTheRocketsCapsuleAndSentToTheOthers()
    {
        VehicleEntity rocket = AddRocket(out PlayerTimeCapsuleEntity capsule);
        PlayerTimeCapsuleMetadata text = new("Hello from 4546B", "");

        await Send(otherPlayer, capsule, text);

        Assert.AreSame(text, capsule.Metadata);
        Assert.IsInstanceOfType<RocketMetadata>(rocket.Metadata, "The capsule's text is its own, not the rocket's.");
        Assert.AreSame(text, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, simulator).Single().NewValue);
        Assert.AreSame(text, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, thirdPlayer).Single().NewValue);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, otherPlayer).Count);
    }

    [TestMethod]
    public async Task TimeCapsuleTextForTheRocketItselfDoesNotReplaceTheRocketsState()
    {
        VehicleEntity rocket = AddRocket(out _);
        EntityMetadata rocketState = rocket.Metadata;

        await Send(otherPlayer, rocket, new PlayerTimeCapsuleMetadata("Hello", ""));

        Assert.AreSame(rocketState, rocket.Metadata);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task FiltrationMachineTimersFromItsSimulatingPlayerAreStoredAndSentToTheOthers()
    {
        InteriorPieceEntity machine = AddFiltrationMachine(simulator);
        FiltrationMachineMetadata timers = new(600f, FiltrationMachineMetadata.IDLE);

        await Send(simulator, machine, timers);

        Assert.AreEqual(600f, ((FiltrationMachineMetadata)machine.Metadata).TimeRemainingWater);
        foreach (Player receiver in new[] { otherPlayer, thirdPlayer })
        {
            Assert.AreEqual(600f, ((FiltrationMachineMetadata)ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, receiver).Single().NewValue).TimeRemainingWater);
        }
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, simulator).Count);
    }

    [TestMethod]
    public async Task FiltrationMachineTimersFromAPlayerWhoDoesNotSimulateItAreNotStored()
    {
        // For example the previous simulating player's last update, which arrives after the machine was handed on
        InteriorPieceEntity machine = AddFiltrationMachine(simulator);
        await Send(simulator, machine, new FiltrationMachineMetadata(300f, 100f));
        packetSender.ClearReceivedCalls();

        await Send(otherPlayer, machine, new FiltrationMachineMetadata(800f, 400f));

        Assert.AreEqual(300f, ((FiltrationMachineMetadata)machine.Metadata).TimeRemainingWater);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task FiltrationMachineTimersAreNotReplacedByAnotherKindOfMetadata()
    {
        InteriorPieceEntity machine = AddFiltrationMachine(simulator);
        await Send(simulator, machine, new FiltrationMachineMetadata(300f, 100f));
        packetSender.ClearReceivedCalls();

        await Send(simulator, machine, new BatteryMetadata(100f));

        Assert.IsInstanceOfType<FiltrationMachineMetadata>(machine.Metadata);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    private InteriorPieceEntity AddFiltrationMachine(Player simulatedBy)
    {
        InteriorPieceEntity machine = InteriorPieceEntity.MakeEmpty();
        machine.Id = new NitroxId();
        machine.TechType = new NitroxTechType("BaseFiltrationMachine");
        entityRegistry.AddEntity(machine);
        Assert.IsTrue(simulationOwnershipData.TryAcquire(machine.Id, simulatedBy, SimulationLockType.TRANSIENT));
        return machine;
    }

    [TestMethod]
    public async Task FirstAnswerAboutATimeCapsuleIsKeptAndSentToTheOthers()
    {
        WorldEntity capsule = AddWorldTimeCapsule();
        TimeCapsuleMetadata answer = new(true, "instance-1", "content-1", "Hi", "From the surface", "a.jpg",
                                         [new TimeCapsuleMetadata.Item(titanium, false, NitroxTechType.None, 0f)]);

        await Send(otherPlayer, capsule, answer);

        Assert.AreSame(answer, capsule.Metadata);
        Assert.AreSame(answer, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, simulator).Single().NewValue);
        Assert.AreSame(answer, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, thirdPlayer).Single().NewValue);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, otherPlayer).Count);
    }

    [TestMethod]
    public async Task LaterAnswerAboutATimeCapsuleGetsTheKeptAnswerBack()
    {
        WorldEntity capsule = AddWorldTimeCapsule();
        TimeCapsuleMetadata first = new(true, "instance-1", "content-1", "Hi", "", "", []);
        await Send(otherPlayer, capsule, first);
        packetSender.ClearReceivedCalls();

        await Send(thirdPlayer, capsule, new TimeCapsuleMetadata(true, "instance-2", "content-2", "Other", "", "", []));

        Assert.AreSame(first, capsule.Metadata);
        Assert.AreSame(first, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, thirdPlayer).Single().NewValue,
            "The later game shows the capsule everybody else sees instead of the one it was given.");
        Assert.AreEqual(1, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task SpotWithoutATimeCapsuleStaysWithoutForEverybody()
    {
        WorldEntity capsule = AddWorldTimeCapsule();
        TimeCapsuleMetadata none = TimeCapsuleMetadata.Empty();
        await Send(otherPlayer, capsule, none);
        packetSender.ClearReceivedCalls();

        await Send(simulator, capsule, new TimeCapsuleMetadata(true, "instance-2", "content-2", "Other", "", "", []));

        Assert.AreSame(none, capsule.Metadata);
        Assert.IsFalse(((TimeCapsuleMetadata)ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, simulator).Single().NewValue).HasCapsule);
    }

    [TestMethod]
    public async Task TimeCapsuleAnswerDoesNotReplaceOtherState()
    {
        ModuleEntity cabinet = AddSource(new MedicalCabinetMetadata(true, true, 900f), simulator);
        EntityMetadata state = cabinet.Metadata;

        await Send(otherPlayer, cabinet, TimeCapsuleMetadata.Empty());

        Assert.AreSame(state, cabinet.Metadata);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    /// <summary>
    ///     A frame's picture is the server's to set when it keeps an upload; a player naming one would skip its limits.
    /// </summary>
    [TestMethod]
    public async Task PictureIsNotSetByAMetadataUpdate()
    {
        ModuleEntity frame = ModuleEntity.MakeEmpty();
        frame.Id = new NitroxId();
        frame.TechType = new NitroxTechType("PictureFrame");
        entityRegistry.AddEntity(frame);

        await Send(otherPlayer, frame, new PictureFrameMetadata(new string('a', 64)));

        Assert.IsNull(frame.Metadata);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    private WorldEntity AddWorldTimeCapsule()
    {
        WorldEntity capsule = new(NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxVector3.One, NitroxTechType.None, 0, "time-capsule", true, new NitroxId(), null);
        entityRegistry.AddEntity(capsule);
        // Every player is near the capsule
        foreach (Player player in new[] { simulator, otherPlayer, thirdPlayer })
        {
            player.OutOfCellVisibleEntities.Add(capsule.Id);
        }
        return capsule;
    }

    private VehicleEntity AddRocket(out PlayerTimeCapsuleEntity capsule)
    {
        VehicleEntity rocket = new(null, 0f, new NitroxTransform(), "", false, new NitroxId(), new NitroxTechType("RocketBase"), new RocketMetadata(1, 0f, 0, 0f, []));
        capsule = new PlayerTimeCapsuleEntity(new NitroxId(), rocket.Id, null);
        rocket.ChildEntities.Add(capsule);
        entityRegistry.AddOrUpdate(rocket);
        return rocket;
    }

    [TestMethod]
    public async Task PilotsPressIsNotUndoneByAHullReportOfTheSimulatingPlayer()
    {
        // The simulating player still simulates the Cyclops (the lock moves a moment later) while the pilot switches the shield on
        VehicleEntity cyclops = AddCyclops(new CyclopsMetadata(false, false, false, true, 1, 1500f, false), simulator);
        otherPlayer.PlayerContext.DrivingVehicle = cyclops.Id;

        await Send(otherPlayer, cyclops, new CyclopsMetadata(false, true, false, true, 1, 1500f, false));
        await Send(simulator, cyclops, new CyclopsMetadata(false, false, false, true, 1, 1200f, false));

        CyclopsMetadata stored = (CyclopsMetadata)cyclops.Metadata;
        Assert.IsTrue(stored.ShieldOn, "The report carried the shield as the simulating player's copy had it before the press.");
        Assert.AreEqual(1200f, stored.Health);
    }

    [TestMethod]
    public async Task HelmControlsOfAPlayerWhoIsNotAtTheHelmAreRefusedAndCorrected()
    {
        VehicleEntity cyclops = AddCyclops(new CyclopsMetadata(false, false, false, true, 1, 1500f, false), simulator);
        simulator.PlayerContext.DrivingVehicle = cyclops.Id;

        await Send(otherPlayer, cyclops, new CyclopsMetadata(true, false, false, false, 2, 1500f, false));

        CyclopsMetadata stored = (CyclopsMetadata)cyclops.Metadata;
        Assert.IsFalse(stored.SilentRunningOn);
        Assert.IsTrue(stored.EngineOn);
        Assert.AreEqual(1, stored.EngineMode);
        CyclopsMetadata correction = (CyclopsMetadata)ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, otherPlayer).Single().NewValue;
        Assert.IsFalse(correction.SilentRunningOn, "The sender shows the press it made until it gets the kept state back.");
    }

    [TestMethod]
    public async Task WithoutPilotTheSimulatingPlayerSetsTheHelmControls()
    {
        // e.g. the game turns the sonar off when nobody pilots
        VehicleEntity cyclops = AddCyclops(new CyclopsMetadata(false, false, true, true, 1, 1500f, false), simulator);

        await Send(simulator, cyclops, new CyclopsMetadata(false, false, false, true, 1, 1500f, false));

        Assert.IsFalse(((CyclopsMetadata)cyclops.Metadata).SonarOn);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, simulator).Count);
    }

    [TestMethod]
    public async Task VehiclesDockedInADestroyedCyclopsAreDestroyedWithIt()
    {
        VehicleEntity cyclops = AddCyclops(new CyclopsMetadata(false, false, false, true, 1, 100f, false), simulator);
        PathBasedChildEntity dock = new("DockingBay", new NitroxId(), null, null, cyclops.Id, []);
        entityRegistry.AddEntity(dock);
        entityRegistry.ReparentEntity(dock, cyclops);
        VehicleEntity seamoth = new(null, 0f, new NitroxTransform(), "", false, new NitroxId(), new NitroxTechType("Seamoth"), null);
        entityRegistry.AddEntity(seamoth);
        entityRegistry.ReparentEntity(seamoth, dock);

        await Send(simulator, cyclops, new CyclopsMetadata(false, false, false, false, 1, 0f, true));

        Assert.IsFalse(entityRegistry.TryGetEntityById(seamoth.Id, out Entity _), "The saved world must not bring it back docked in the wreck.");
        Assert.IsTrue(entityRegistry.TryGetEntityById(dock.Id, out Entity _));
        foreach (Player player in new[] { simulator, otherPlayer, thirdPlayer })
        {
            Assert.AreEqual(seamoth.Id, ConnectedPlayers.SentTo<EntityDestroyed>(packetSender, player).Single().Id);
        }
    }

    [TestMethod]
    public async Task AnotherPlayersCopyCannotDestroyTheCyclopsNorItsDockedVehicles()
    {
        VehicleEntity cyclops = AddCyclops(new CyclopsMetadata(false, false, false, true, 1, 100f, false), simulator);
        VehicleEntity seamoth = new(null, 0f, new NitroxTransform(), "", false, new NitroxId(), new NitroxTechType("Seamoth"), null);
        entityRegistry.AddEntity(seamoth);
        entityRegistry.ReparentEntity(seamoth, cyclops);

        await Send(otherPlayer, cyclops, new CyclopsMetadata(false, false, false, false, 1, 0f, true));

        Assert.IsFalse(((CyclopsMetadata)cyclops.Metadata).IsDestroyed);
        Assert.IsTrue(entityRegistry.TryGetEntityById(seamoth.Id, out Entity _));
    }

    private VehicleEntity AddCyclops(CyclopsMetadata metadata, Player simulatedBy)
    {
        VehicleEntity cyclops = new(null, 0f, new NitroxTransform(), "", false, new NitroxId(), new NitroxTechType("Cyclops"), metadata);
        entityRegistry.AddOrUpdate(cyclops);
        Assert.IsTrue(simulationOwnershipData.TryAcquire(cyclops.Id, simulatedBy, SimulationLockType.EXCLUSIVE));
        return cyclops;
    }

    [TestMethod]
    public async Task FruitPickedByOnePlayerIsNotBroughtBackByTheBaseSimulatorsOlderCopy()
    {
        InventoryItemEntity plant = AddPlant(new PlantableMetadata(5f, 0, new FruitPlantMetadata([false, false], 100f), PlantableMetadata.NO_HEALTH), simulator);

        await Send(otherPlayer, plant, new PlantableMetadata(5f, 0, new FruitPlantMetadata([true, false], 300f), PlantableMetadata.NO_HEALTH));
        packetSender.ClearReceivedCalls();
        await Send(simulator, plant, new PlantableMetadata(5f, 0, new FruitPlantMetadata([false, false], 100f), PlantableMetadata.NO_HEALTH));

        CollectionAssert.AreEqual(new[] { true, false }, ((PlantableMetadata)plant.Metadata).FruitPlantMetadata.PickedStates);
        PlantableMetadata echoed = (PlantableMetadata)ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, simulator).Single().NewValue;
        CollectionAssert.AreEqual(new[] { true, false }, echoed.FruitPlantMetadata.PickedStates, "The simulating player must hide the fruit too, or it is picked a second time.");
    }

    [TestMethod]
    public async Task OnlyTheBaseSimulatorGrowsFruitBack()
    {
        InventoryItemEntity plant = AddPlant(new PlantableMetadata(5f, 0, new FruitPlantMetadata([true, true], 100f), PlantableMetadata.NO_HEALTH), simulator);

        await Send(otherPlayer, plant, new PlantableMetadata(5f, 0, new FruitPlantMetadata([false, true], 100f), PlantableMetadata.NO_HEALTH));
        CollectionAssert.AreEqual(new[] { true, true }, ((PlantableMetadata)plant.Metadata).FruitPlantMetadata.PickedStates);

        await Send(simulator, plant, new PlantableMetadata(5f, 0, new FruitPlantMetadata([false, true], 250f), PlantableMetadata.NO_HEALTH));
        CollectionAssert.AreEqual(new[] { false, true }, ((PlantableMetadata)plant.Metadata).FruitPlantMetadata.PickedStates);
        Assert.AreEqual(250f, ((PlantableMetadata)plant.Metadata).FruitPlantMetadata.TimeNextFruit);
    }

    [TestMethod]
    public async Task PlantHealthOnlyGoesDownExceptFromTheBaseSimulator()
    {
        InventoryItemEntity plant = AddPlant(new PlantableMetadata(5f, 2, null, 80f), simulator);

        await Send(otherPlayer, plant, new PlantableMetadata(5f, 2, null, 100f));
        Assert.AreEqual(80f, ((PlantableMetadata)plant.Metadata).Health);
        await Send(otherPlayer, plant, new PlantableMetadata(5f, 2, null, 60f));
        Assert.AreEqual(60f, ((PlantableMetadata)plant.Metadata).Health);
        await Send(simulator, plant, new PlantableMetadata(5f, 2, null, 70f));
        Assert.AreEqual(70f, ((PlantableMetadata)plant.Metadata).Health);
        Assert.AreEqual(2, ((PlantableMetadata)plant.Metadata).SlotID);
    }

    private InventoryItemEntity AddPlant(PlantableMetadata metadata, Player baseSimulator)
    {
        BuildEntity builtBase = BuildEntity.MakeEmpty();
        builtBase.Id = new NitroxId();
        entityRegistry.AddEntity(builtBase);
        Assert.IsTrue(simulationOwnershipData.TryAcquire(builtBase.Id, baseSimulator, SimulationLockType.TRANSIENT));
        ModuleEntity planter = ModuleEntity.MakeEmpty();
        planter.Id = new NitroxId();
        planter.ParentId = builtBase.Id;
        entityRegistry.AddEntity(planter);
        InventoryItemEntity plant = new(new NitroxId(), "seed", new NitroxTechType("MelonSeed"), metadata, planter.Id, [], false);
        entityRegistry.AddEntity(plant);
        return plant;
    }

    private Task Send(Player sender, Entity entity, EntityMetadata metadata)
    {
        return processor.Process(new AuthProcessorContext(sender, packetSender), new EntityMetadataUpdate(entity.Id, metadata));
    }

    private MapRoomEntity AddRoom(MapRoomMetadata scanState, Player simulatedBy)
    {
        MapRoomEntity room = new(new NitroxId(), new NitroxId(), new NitroxInt3(1, 0, 2)) { Metadata = scanState };
        entityRegistry.AddEntity(room);
        Assert.IsTrue(simulationOwnershipData.TryAcquire(room.Id, simulatedBy, SimulationLockType.TRANSIENT));
        return room;
    }

    private GlobalRootEntity AddCamera(MapRoomCameraMetadata cameraState, MapRoomEntity dockedIn, Player simulatedBy)
    {
        GlobalRootEntity camera = ScannerRoomScenarioFixture.CreateCamera(new NitroxId(), dockedIn?.Id, cameraState);
        dockedIn?.ChildEntities.Add(camera);
        entityRegistry.AddEntity(camera);
        Assert.IsTrue(simulationOwnershipData.TryAcquire(camera.Id, simulatedBy, SimulationLockType.TRANSIENT));
        return camera;
    }

    private ModuleEntity AddSource(EntityMetadata metadata, Player simulatedBy)
    {
        ModuleEntity source = ModuleEntity.MakeEmpty();
        source.Id = new NitroxId();
        source.Metadata = metadata;
        entityRegistry.AddEntity(source);
        Assert.IsTrue(simulationOwnershipData.TryAcquire(source.Id, simulatedBy, SimulationLockType.TRANSIENT));
        return source;
    }
    [TestMethod]
    public async Task EquipmentThePlayerChangesInARunningGameIsStoredAsTheirsAndShownToTheOthers()
    {
        PlayerEntity body = AddBody(simulator, null);
        simulator.HasFinishedInitialSync = true;
        NitroxId tank = new();

        await Send(simulator, body, Worn(tank));

        simulator.EquippedItems.Should().ContainKey("Tank").WhoseValue.Should().Be(tank);
        ((PlayerMetadata)body.Metadata).EquippedItems.Single().Id.Should().Be(tank);
        ((PlayerMetadata)ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, otherPlayer).Single().NewValue).EquippedItems.Single().Id.Should().Be(tank);
    }

    [TestMethod]
    public async Task EquipmentSentWhileThePlayerIsStillJoiningDoesNotEmptyTheirList()
    {
        // Rejoin 1 of the live test: the joining game put the tank on twice and reported the first removal
        NitroxId tank = new();
        PlayerEntity body = AddBody(simulator, Worn(tank));
        simulator.EquippedItems["Tank"] = tank;
        simulator.HasFinishedInitialSync = false;

        await Send(simulator, body, new PlayerMetadata([]));

        simulator.EquippedItems.Should().ContainKey("Tank");
        ((PlayerMetadata)body.Metadata).EquippedItems.Should().ContainSingle();
        ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, otherPlayer).Should().BeEmpty();
    }

    [TestMethod]
    public async Task EquipmentOfAnotherPlayersBodyIsNotStored()
    {
        PlayerEntity body = AddBody(simulator, null);
        otherPlayer.HasFinishedInitialSync = true;

        await Send(otherPlayer, body, Worn(new NitroxId()));

        simulator.EquippedItems.Should().BeEmpty();
        body.Metadata.Should().BeNull();
    }

    [TestMethod]
    public async Task LungsOxygenFromADamageReportDoesNotTakeThePlaceOfTheEquipmentList()
    {
        NitroxId tank = new();
        PlayerEntity body = AddBody(simulator, Worn(tank));
        simulator.HasFinishedInitialSync = true;
        otherPlayer.HasFinishedInitialSync = true;

        await Send(simulator, body, new OxygenMetadata(39f));
        await Send(otherPlayer, body, new OxygenMetadata(45f));

        body.Metadata.Should().BeOfType<PlayerMetadata>();
        ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, thirdPlayer).Should().BeEmpty();
    }

    private static PlayerMetadata Worn(NitroxId tank) => new([new PlayerMetadata.EquippedItem(tank, "Tank", new NitroxTechType("Tank"))]);

    private PlayerEntity AddBody(Player owner, PlayerMetadata? metadata)
    {
        PlayerEntity body = new(new NitroxTransform(), 0, null, false, owner.GameObjectId, NitroxTechType.None, metadata, null, []);
        entityRegistry.AddOrUpdate(body);
        return body;
    }
}
