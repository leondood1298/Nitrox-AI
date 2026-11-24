using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities.Spawning;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.Helper;
using NSubstitute;

namespace Nitrox.Test.Server.GameLogic.Entities;

[TestClass]
public sealed class WorldEntityManagerTest
{
    private static readonly NitroxInt3 batchId = new(10, 18, 10);

    private EntityRegistry registry = null!;
    private FakeGenerator generator = null!;
    private WorldEntityManager manager = null!;

    [TestInitialize]
    public void Setup()
    {
        registry = new EntityRegistry(NullLogger<EntityRegistry>.Instance);
        generator = new FakeGenerator();
        manager = new WorldEntityManager(Substitute.For<IPacketSender>(), registry, generator, new ConnectedPlayers().PlayerManager, null!, NullLogger<WorldEntityManager>.Instance,
                                         new SpawnedBatches());
    }

    [TestMethod]
    public async Task ASaveTakenWhileABatchIsBeingMadeListsNeitherTheBatchNorItsEntities()
    {
        TaskCompletionSource<List<Entity>> generation = generator.Next();
        Task<int> loading = manager.LoadUnspawnedEntitiesAsync(batchId, true);

        (List<NitroxInt3> batchesDuring, List<Entity> entitiesDuring) = manager.SnapshotForSave();
        WorldEntity fish = NewFish();
        generation.SetResult([fish]);
        await loading;
        (List<NitroxInt3> batchesAfter, List<Entity> entitiesAfter) = manager.SnapshotForSave();

        batchesDuring.Should().NotContain(batchId);
        entitiesDuring.Should().BeEmpty();
        batchesAfter.Should().Contain(batchId);
        entitiesAfter.Should().ContainSingle().Which.Id.Should().Be(fish.Id);
    }

    [TestMethod]
    public async Task ABatchWhoseMakingFailedIsMadeAgainTheNextTimeItIsLoaded()
    {
        generator.Next().SetException(new InvalidOperationException("broken batch file"));
        Func<Task> firstLoad = () => manager.LoadUnspawnedEntitiesAsync(batchId, true);
        await firstLoad.Should().ThrowAsync<InvalidOperationException>();
        manager.SnapshotForSave().SpawnedBatches.Should().NotContain(batchId);

        WorldEntity fish = NewFish();
        generator.Next().SetResult([fish]);
        int registered = await manager.LoadUnspawnedEntitiesAsync(batchId, true);

        registered.Should().Be(1);
        registry.GetEntityById(fish.Id).HasValue.Should().BeTrue();
        manager.SnapshotForSave().SpawnedBatches.Should().Contain(batchId);
    }

    [TestMethod]
    public async Task ADoneBatchIsNotMadeAgain()
    {
        generator.Next().SetResult([NewFish()]);
        await manager.LoadUnspawnedEntitiesAsync(batchId, true);

        int second = await manager.LoadUnspawnedEntitiesAsync(batchId, true);

        second.Should().Be(0);
        generator.Calls.Should().Be(1);
    }

    [TestMethod]
    public async Task TwoPlayersLoadingTheSameBatchAtOnceShareOneMaking()
    {
        TaskCompletionSource<List<Entity>> generation = generator.Next();
        Task<int> first = manager.LoadUnspawnedEntitiesAsync(batchId, true);
        Task<int> second = manager.LoadUnspawnedEntitiesAsync(batchId, true);
        generation.SetResult([NewFish()]);

        (await first).Should().Be(1);
        (await second).Should().Be(1);
        generator.Calls.Should().Be(1);
    }

    [TestMethod]
    public async Task ABatchThatCameOutEmptyIsNotSaved()
    {
        generator.Next().SetResult([]);
        await manager.LoadUnspawnedEntitiesAsync(batchId, true);

        manager.SnapshotForSave().SpawnedBatches.Should().BeEmpty();
        (await manager.LoadUnspawnedEntitiesAsync(batchId, true)).Should().Be(0);
        generator.Calls.Should().Be(1);
    }

    private static WorldEntity NewFish()
    {
        AbsoluteEntityCell cell = new(batchId, new NitroxInt3(1, 1, 1), 0);
        return new WorldEntity(new NitroxVector3(cell.Center.X, cell.Center.Y, cell.Center.Z), NitroxQuaternion.Identity, NitroxVector3.One, new NitroxTechType("Peeper"), 0, "peeper", true, new NitroxId(), null);
    }

    private sealed class FakeGenerator : IBatchEntityGenerator
    {
        private readonly Queue<TaskCompletionSource<List<Entity>>> generations = [];

        public int Calls { get; private set; }

        public TaskCompletionSource<List<Entity>> Next()
        {
            TaskCompletionSource<List<Entity>> generation = new(TaskCreationOptions.RunContinuationsAsynchronously);
            generations.Enqueue(generation);
            return generation;
        }

        public Task<List<Entity>> GenerateAsync(NitroxInt3 batchId, bool fullCacheCreation)
        {
            Calls++;
            return generations.Dequeue().Task;
        }
    }
}
