using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Test.Server.Helper;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public sealed class EntityTransformUpdatesProcessorTest
{
    private static readonly NitroxVector3 inFirstCell = new(-500f, -100f, -500f);

    private ItemClaimScenario scenario = null!;
    private EntityTransformUpdatesProcessor processor = null!;
    private WorldEntity fish = null!;
    private AbsoluteEntityCell firstCell = null!;
    private AbsoluteEntityCell nextCell = null!;
    private NitroxVector3 inNextCell;

    [TestInitialize]
    public void Setup()
    {
        scenario = new ItemClaimScenario();
        processor = new EntityTransformUpdatesProcessor(scenario.Players.PlayerManager, scenario.WorldEntityManager, scenario.Ownership);
        fish = new WorldEntity(inFirstCell, NitroxQuaternion.Identity, NitroxVector3.One, new NitroxTechType("Peeper"), 0, "peeper", true, new NitroxId(), null);
        scenario.EntityRegistry.AddOrUpdate(fish);
        scenario.WorldEntityManager.RegisterWorldEntity(fish);
        firstCell = fish.AbsoluteEntityCell;
        inNextCell = inFirstCell + new NitroxVector3(firstCell.GetCellSize().X, 0, 0);
        nextCell = new AbsoluteEntityCell(inNextCell, 0);
        scenario.Ownership.TryAcquire(fish.Id, scenario.PlayerA, SimulationLockType.TRANSIENT);
        scenario.PlayerA.AddCells([firstCell, nextCell]);
    }

    [TestMethod]
    public async Task APlayerWhoSawTheEntityLeaveTheirCellsHearsOfTheMoveThatTookItAway()
    {
        scenario.PlayerB.AddCells([firstCell]);

        await processor.Process(scenario.Context(scenario.PlayerA), Move(inNextCell));
        await processor.Process(scenario.Context(scenario.PlayerA), Move(inNextCell + new NitroxVector3(0.5f, 0, 0)));

        // The move out, so their game takes it away; not the moves after it
        scenario.SentTo<EntityTransformUpdates>(scenario.PlayerB).Should().ContainSingle().Which.Updates.Single().Position.Should().Be(inNextCell);
    }

    [TestMethod]
    public async Task APlayerWhoNeverSawTheEntityHearsNothing()
    {
        await processor.Process(scenario.Context(scenario.PlayerA), Move(inNextCell));

        scenario.SentTo<EntityTransformUpdates>(scenario.PlayerB).Should().BeEmpty();
    }

    [TestMethod]
    public async Task APlayerTheEntityComesToIsGivenTheEntity()
    {
        scenario.PlayerB.AddCells([nextCell]);

        await processor.Process(scenario.Context(scenario.PlayerA), Move(inNextCell));

        scenario.SentTo<SpawnEntities>(scenario.PlayerB).Should().ContainSingle().Which.Entities.Single().Id.Should().Be(fish.Id);
    }

    private EntityTransformUpdates Move(NitroxVector3 position) => new([new EntityTransformUpdates.RawTransformUpdate(fish.Id, position, NitroxQuaternion.Identity)]);
}
