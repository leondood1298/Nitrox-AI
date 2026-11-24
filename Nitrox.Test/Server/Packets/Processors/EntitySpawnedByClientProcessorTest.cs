using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Test.Server.GameLogic;
using Nitrox.Test.Server.Helper;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public sealed class EntitySpawnedByClientProcessorTest
{
    private ScannerRoomScenarioFixture fixture;
    private WorldEntity crashFish;

    [TestInitialize]
    public void Init()
    {
        fixture = new ScannerRoomScenarioFixture(connectedPlayers: new ConnectedPlayers());
        crashFish = new WorldEntity(new NitroxVector3(10f, -20f, 30f), NitroxQuaternion.Identity, NitroxVector3.One, new NitroxTechType("Crash"), 0, "crash", false,
                                    new NitroxId(), null);
        foreach (Player player in new[] { fixture.PlayerA, fixture.PlayerB })
        {
            player.AddCells([crashFish.AbsoluteEntityCell]);
        }
    }

    [TestMethod]
    public async Task TheSpawningPlayerIsToldItSimulatesWhatItMade()
    {
        await fixture.SpawnByClientAsync(fixture.PlayerA, crashFish);

        List<SimulationOwnershipChange> toSpawner = fixture.SentTo<SimulationOwnershipChange>(fixture.PlayerA);
        Assert.AreEqual(1, toSpawner.Count);
        SimulatedEntity simulated = toSpawner[0].Entities.Single();
        Assert.AreEqual(crashFish.Id, simulated.Id);
        Assert.AreEqual(fixture.PlayerA.SessionId, simulated.SessionId);
        Assert.IsTrue(simulated.ChangesPosition);
        Assert.AreEqual(fixture.PlayerA, fixture.Ownership.GetPlayerForLock(crashFish.Id));
    }

    [TestMethod]
    public async Task TheOthersAreStillToldWhoSimulatesIt()
    {
        await fixture.SpawnByClientAsync(fixture.PlayerA, crashFish);

        Assert.AreEqual(1, fixture.SentToOthers<SimulationOwnershipChange>().Count);
        Assert.AreEqual(1, fixture.SentTo<SpawnEntities>(fixture.PlayerB).Count);
        Assert.AreEqual(0, fixture.SentTo<SpawnEntities>(fixture.PlayerA).Count);
    }

    [TestMethod]
    public async Task AnEntityThatNeedsNoSimulationIsGivenToNobody()
    {
        CreatureRespawnEntity respawner = new(crashFish.Transform, 0, "respawn", false, new NitroxId(), NitroxTechType.None, null, null, [], 100f, new NitroxTechType("Peeper"), []);

        await fixture.SpawnByClientAsync(fixture.PlayerA, respawner, false);

        Assert.IsNull(fixture.Ownership.GetPlayerForLock(respawner.Id));
        Assert.AreEqual(0, fixture.Sent<SimulationOwnershipChange>().Count);
    }

    [TestMethod]
    public async Task OfTwoPlayersFillingTheSameSlotTheFirstGetsIt()
    {
        ItemClaimScenario scenario = new();
        EntitySpawnedByClientProcessor processor = CreateProcessor(scenario);
        InventoryItemEntity alicesBattery = scenario.AddItemIn(scenario.PlayerA.GameObjectId);
        InventoryItemEntity bobsBattery = scenario.AddItemIn(scenario.PlayerB.GameObjectId);

        await processor.Process(scenario.Context(scenario.PlayerA), new EntitySpawnedByClient(new InstalledModuleEntity("BatteryCharger1", "battery", alicesBattery.Id, alicesBattery.TechType, null,
            scenario.Locker.Id, []), true));
        await processor.Process(scenario.Context(scenario.PlayerB), new EntitySpawnedByClient(new InstalledModuleEntity("BatteryCharger1", "battery", bobsBattery.Id, bobsBattery.TechType, null,
            scenario.Locker.Id, []), true));

        scenario.Locker.ChildEntities.OfType<InstalledModuleEntity>().Should().ContainSingle().Which.Id.Should().Be(alicesBattery.Id);
        // Bob's battery goes back to his inventory
        scenario.EntityRegistry.GetEntityById(bobsBattery.Id).Value.Should().BeOfType<InventoryItemEntity>().Which.ParentId.Should().Be(scenario.PlayerB.GameObjectId);
        scenario.SentTo<SpawnEntities>(scenario.PlayerB).Should().Contain(spawn => spawn.ForceRespawn && spawn.Entities.Single().Id == bobsBattery.Id);
        scenario.SentTo<ItemClaimLost>(scenario.PlayerB).Single().WinnerName.Should().Be("Alice");
    }

    [TestMethod]
    public async Task APlayerCannotDropAnItemAnotherPlayerHolds()
    {
        ItemClaimScenario scenario = new();
        EntitySpawnedByClientProcessor processor = CreateProcessor(scenario);
        InventoryItemEntity item = scenario.AddItemIn(scenario.PlayerA.GameObjectId);
        WorldEntity dropped = new(new NitroxVector3(1, 2, 3), NitroxQuaternion.Identity, NitroxVector3.One, item.TechType, 0, item.ClassId, false, item.Id, null);

        await processor.Process(scenario.Context(scenario.PlayerB), new EntitySpawnedByClient(dropped, true));

        scenario.EntityRegistry.GetEntityById(item.Id).Value.Should().BeSameAs(item);
    }

    [TestMethod]
    public async Task TheResourcesOfAnOutcropTwoPlayersBrokeGoToTheFirst()
    {
        ItemClaimScenario scenario = new();
        EntitySpawnedByClientProcessor processor = CreateProcessor(scenario);
        ItemSource outcrop = new(new NitroxId(), "break");
        WorldEntity alicesTitanium = NewResource();
        WorldEntity alicesCopper = NewResource();
        WorldEntity bobsTitanium = NewResource();

        await processor.Process(scenario.Context(scenario.PlayerA), new EntitySpawnedByClient(alicesTitanium, true, true, outcrop));
        await processor.Process(scenario.Context(scenario.PlayerB), new EntitySpawnedByClient(bobsTitanium, true, true, outcrop));
        await processor.Process(scenario.Context(scenario.PlayerA), new EntitySpawnedByClient(alicesCopper, true, true, outcrop));

        scenario.EntityRegistry.GetEntityById(alicesTitanium.Id).HasValue.Should().BeTrue();
        scenario.EntityRegistry.GetEntityById(alicesCopper.Id).HasValue.Should().BeTrue();
        scenario.EntityRegistry.GetEntityById(bobsTitanium.Id).HasValue.Should().BeFalse();
        scenario.SentTo<ItemClaimLost>(scenario.PlayerB).Single().RemoveLocalCopy.Should().BeTrue();
    }

    [TestMethod]
    public async Task OfTwoSeedsPlantedInOneSlotTheFirstKeepsItAndTheLaterIsMovedByItsGame()
    {
        ItemClaimScenario scenario = new();
        EntitySpawnedByClientProcessor processor = CreateProcessor(scenario);
        InventoryItemEntity alicesSeed = scenario.AddItemIn(scenario.PlayerA.GameObjectId);
        InventoryItemEntity bobsSeed = scenario.AddItemIn(scenario.PlayerB.GameObjectId);

        await processor.Process(scenario.Context(scenario.PlayerA), new EntitySpawnedByClient(Planted(alicesSeed, scenario.Locker.Id, 0), true));
        await processor.Process(scenario.Context(scenario.PlayerB), new EntitySpawnedByClient(Planted(bobsSeed, scenario.Locker.Id, 0), true));

        scenario.Locker.ChildEntities.Should().ContainSingle(child => child.Metadata is PlantableMetadata).Which.Id.Should().Be(alicesSeed.Id);
        scenario.EntityRegistry.GetEntityById(bobsSeed.Id).Value.ParentId.Should().Be(scenario.PlayerB.GameObjectId, "Bob's seed is still his until his game plants it again");
        PlanterSlotTaken taken = scenario.SentTo<PlanterSlotTaken>(scenario.PlayerB).Single();
        taken.PlantableId.Should().Be(bobsSeed.Id);
        taken.SlotId.Should().Be(0);
        scenario.SentTo<SpawnEntities>(scenario.PlayerA).Should().NotContain(spawn => spawn.Entities.Any(entity => entity.Id == bobsSeed.Id));

        await processor.Process(scenario.Context(scenario.PlayerB), new EntitySpawnedByClient(Planted(bobsSeed, scenario.Locker.Id, 1), true));
        scenario.Locker.ChildEntities.Count(child => child.Metadata is PlantableMetadata).Should().Be(2);
    }

    private static InventoryItemEntity Planted(InventoryItemEntity seed, NitroxId planterId, int slot) =>
        new(seed.Id, seed.ClassId, seed.TechType, new PlantableMetadata(5f, slot), planterId, [], false);

    private static WorldEntity NewResource() => new(new NitroxVector3(1, -2, 3), NitroxQuaternion.Identity, NitroxVector3.One, new NitroxTechType("Titanium"), 0,
        ItemClaimScenario.TITANIUM_CLASS_ID, false, new NitroxId(), null);

    private static EntitySpawnedByClientProcessor CreateProcessor(ItemClaimScenario scenario)
    {
        EntitySimulation entitySimulation = new(scenario.PacketSender, scenario.EntityRegistry, scenario.WorldEntityManager, scenario.Ownership, scenario.Players.PlayerManager,
            scenario.ScannerRoomManager, NullLogger<EntitySimulation>.Instance);
        return new EntitySpawnedByClientProcessor(scenario.Players.PlayerManager, scenario.EntityRegistry, scenario.WorldEntityManager, entitySimulation, scenario.ScannerRoomManager,
            scenario.ItemClaims);
    }
}
