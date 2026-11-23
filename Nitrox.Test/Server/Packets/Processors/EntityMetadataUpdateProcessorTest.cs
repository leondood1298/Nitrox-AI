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
///     merged with another repair state, never replaced.
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
        processor = new EntityMetadataUpdateProcessor(players.PlayerManager, entityRegistry, simulationOwnershipData, scannerRoomManager, basePowerManager,
            NullLogger<EntityMetadataUpdateProcessor>.Instance);
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
}
