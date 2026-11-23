using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace NitroxClient.GameLogic.Spawning.Metadata;

/// <summary>
///     An entity spawns some time after its spawn packet arrived. Whatever update arrives until it has finished
///     spawning is newer than the state in that packet and must be what the entity ends up with.
/// </summary>
[TestClass]
public sealed class EntityMetadataManagerTest
{
    private readonly EntityMetadataManager manager = new([], []);
    private readonly NitroxId entityId = new();
    private readonly BatteryMetadata fromSpawnPacket = new(10f);
    private readonly BatteryMetadata update = new(20f);

    [TestMethod]
    public void SpawnPacketStateIsUsedWhenNothingNewerArrived()
    {
        Assert.AreSame(fromSpawnPacket, manager.TakeNewest(entityId, fromSpawnPacket, spawnContinues: false));
    }

    [TestMethod]
    public void UpdateReceivedBeforeTheEntitySpawnedStandsInForTheSpawnPacketState()
    {
        manager.RegisterNewerMetadata(entityId, update);

        Assert.AreSame(update, manager.TakeNewest(entityId, fromSpawnPacket, spawnContinues: false));
        Assert.AreSame(fromSpawnPacket, manager.TakeNewest(entityId, fromSpawnPacket, spawnContinues: false), "It is used for one spawn only.");
    }

    [TestMethod]
    public void LaterUpdateReplacesAnEarlierOne()
    {
        BatteryMetadata laterUpdate = new(30f);
        manager.RegisterNewerMetadata(entityId, update);
        manager.RegisterNewerMetadata(entityId, laterUpdate);

        Assert.AreSame(laterUpdate, manager.TakeNewest(entityId, fromSpawnPacket, spawnContinues: false));
    }

    [TestMethod]
    public void OnlyAnUpdateOfTheSameKindStandsIn()
    {
        manager.RegisterNewerMetadata(entityId, new ThermalPlantMetadata(60f));
        manager.RegisterNewerMetadata(new NitroxId(), update);

        Assert.AreSame(fromSpawnPacket, manager.TakeNewest(entityId, fromSpawnPacket, spawnContinues: false));
    }

    [TestMethod]
    public void FinalApplicationDoesNotPutTheSpawnPacketStateBack()
    {
        // Base pieces get their metadata before their children spawn and once more when they are done.
        manager.RegisterNewerMetadata(entityId, update);

        Assert.AreSame(update, manager.TakeNewest(entityId, fromSpawnPacket, spawnContinues: true));
        Assert.AreSame(update, manager.TakeNewest(entityId, fromSpawnPacket, spawnContinues: false));
        Assert.IsFalse(manager.IsAwaitingSpawnEnd(entityId, fromSpawnPacket));
    }

    [TestMethod]
    public void UpdateArrivingWhileTheChildrenSpawnIsWhatTheEntityEndsWith()
    {
        Assert.AreSame(fromSpawnPacket, manager.TakeNewest(entityId, fromSpawnPacket, spawnContinues: true));
        Assert.IsTrue(manager.IsAwaitingSpawnEnd(entityId, update), "Tells the packet processor to keep updates until the spawn has ended.");

        manager.RegisterNewerMetadata(entityId, update);

        Assert.AreSame(update, manager.TakeNewest(entityId, fromSpawnPacket, spawnContinues: false));
    }

    [TestMethod]
    public void NothingIsAwaitedForAnEntityThatIsNotSpawning()
    {
        Assert.IsFalse(manager.IsAwaitingSpawnEnd(entityId, update));
        manager.TakeNewest(entityId, fromSpawnPacket, spawnContinues: false);
        Assert.IsFalse(manager.IsAwaitingSpawnEnd(entityId, update));
    }

    [TestMethod]
    public void ClearedUpdatesAreNotUsedForALaterSpawn()
    {
        manager.RegisterNewerMetadata(entityId, update);

        manager.ClearNewerMetadata();

        Assert.AreSame(fromSpawnPacket, manager.TakeNewest(entityId, fromSpawnPacket, spawnContinues: false));
    }
}
