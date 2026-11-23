using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nitrox.Model.Configuration;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.Factories;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Which Lifepod a player starts in. A pod handed back as newly created is sent as a whole to every player in the
///     game, so only a pod that was really made for the player may come back that way. A pod has an entity for each of
///     its power cells, also one loaded from an older save.
/// </summary>
[TestClass]
public sealed class EscapePodManagerTest
{
    private EscapePodManager manager;
    private EntityRegistry entityRegistry;
    private EscapePodEntity knownPod;

    [TestInitialize]
    public async Task Init()
    {
        IOptions<SubnauticaServerOptions> options = Options.Create(new SubnauticaServerOptions { Seed = "TESTSEED" });
        entityRegistry = new EntityRegistry(NullLogger<EntityRegistry>.Instance);
        // The start positions come from the game's files, which a pod with a free place does not need.
        manager = new EscapePodManager(new RandomFactory(options), entityRegistry, null, options, Substitute.For<IPacketSender>());
        // As loaded from a save made before the cells were synchronized
        knownPod = new EscapePodEntity(NitroxVector3.Zero, new NitroxId(), new EscapePodMetadata(true, true));
        knownPod.Players.Add(new PeerId(1));
        entityRegistry.AddOrUpdate(knownPod);
        await manager.AddKnownPodsAsync([knownPod]);
    }

    [TestMethod]
    public void ANewPodGetsAnEntityForEachOfItsPowerCells()
    {
        // What CreateNewEscapePodAsync, which needs the game's files for the start position, gives every new pod.
        EscapePodEntity pod = new(NitroxVector3.Zero, new NitroxId(), new EscapePodMetadata(false, false));

        EscapePodManager.AddMissingPowerCells(pod);

        List<EscapePodPowerCellEntity> cells = pod.ChildEntities.OfType<EscapePodPowerCellEntity>().ToList();
        cells.Select(cell => cell.CellIndex).Should().BeEquivalentTo([0, 1, 2]);
        cells.Should().OnlyContain(cell => cell.ParentId == pod.Id && cell.Metadata == null, "A cell keeps the prefab's level until its first report.");
        cells.Select(cell => cell.Id).Should().OnlyHaveUniqueItems();
    }

    [TestMethod]
    public void APodLoadedWithoutPowerCellsGetsThemRegistered()
    {
        List<EscapePodPowerCellEntity> cells = knownPod.ChildEntities.OfType<EscapePodPowerCellEntity>().ToList();

        cells.Should().HaveCount(EscapePodManager.POWER_CELLS_PER_ESCAPEPOD);
        foreach (EscapePodPowerCellEntity cell in cells)
        {
            entityRegistry.TryGetEntityById(cell.Id, out EscapePodPowerCellEntity registered).Should().BeTrue();
            registered.Should().BeSameAs(cell);
        }
    }

    [TestMethod]
    public void APodThatHasSomeOfItsCellsKeepsThemAndGetsTheRest()
    {
        EscapePodEntity pod = new(NitroxVector3.Zero, new NitroxId(), null);
        EscapePodPowerCellEntity stored = new(new NitroxId(), 1, pod.Id) { Metadata = new PowerSourceMetadata(12f, 0f, BasePowerSourceType.ESCAPE_POD) };
        pod.ChildEntities.Add(stored);

        List<EscapePodPowerCellEntity> added = EscapePodManager.AddMissingPowerCells(pod);

        added.Select(cell => cell.CellIndex).Should().BeEquivalentTo([0, 2]);
        pod.ChildEntities.Should().Contain(stored);
        EscapePodManager.AddMissingPowerCells(pod).Should().BeEmpty("Loading the same pod again adds nothing.");
    }

    [TestMethod]
    public async Task ANewPlayerPutInAPodThatWasThereGetsNoNewlyCreatedPod()
    {
        (NitroxId podId, EscapePodEntity newlyCreatedPod) = await manager.AssignPlayerToEscapePodAsync(new PeerId(2));

        podId.Should().Be(knownPod.Id);
        newlyCreatedPod.Should().BeNull();
        knownPod.Players.Should().Contain(new PeerId(2));
    }

    [TestMethod]
    public async Task APlayerWhoHasAPodGetsTheSamePodAndNoNewlyCreatedOne()
    {
        (NitroxId podId, EscapePodEntity newlyCreatedPod) = await manager.AssignPlayerToEscapePodAsync(new PeerId(1));

        podId.Should().Be(knownPod.Id);
        newlyCreatedPod.Should().BeNull();
        knownPod.Players.Should().HaveCount(1);
    }
}
