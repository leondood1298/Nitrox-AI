namespace NitroxClient.Communication.Packets.Processors;

[TestClass]
public sealed class EntityMetadataUpdateProcessorTest
{
    [TestMethod]
    public void UpdateForAnEntityThatHasNotSpawnedYetIsKept()
    {
        // While joining, the bases spawn outside the spawn queue. An update dropped here would leave the entity with
        // the older state from the join packet.
        Assert.IsTrue(EntityMetadataUpdateProcessor.MustKeepForSpawn(spawningEntities: false, rebuildingBases: false, objectExists: false, awaitingSpawnEnd: false));
    }

    [TestMethod]
    public void UpdateForAnEntityWhoseChildrenStillSpawnIsKept()
    {
        Assert.IsTrue(EntityMetadataUpdateProcessor.MustKeepForSpawn(spawningEntities: false, rebuildingBases: false, objectExists: true, awaitingSpawnEnd: true));
    }

    [TestMethod]
    public void UpdateDuringABuildingResyncIsKept()
    {
        // The object it is applied to is about to be destroyed and spawned again from the resync packet.
        Assert.IsTrue(EntityMetadataUpdateProcessor.MustKeepForSpawn(spawningEntities: false, rebuildingBases: true, objectExists: true, awaitingSpawnEnd: false));
    }

    [TestMethod]
    public void UpdateWhileTheSpawnQueueRunsIsKept()
    {
        Assert.IsTrue(EntityMetadataUpdateProcessor.MustKeepForSpawn(spawningEntities: true, rebuildingBases: false, objectExists: true, awaitingSpawnEnd: false));
    }

    [TestMethod]
    public void UpdateForASpawnedEntityIsOnlyApplied()
    {
        // Kept, it could stand in for a newer state the next time the entity spawns.
        Assert.IsFalse(EntityMetadataUpdateProcessor.MustKeepForSpawn(spawningEntities: false, rebuildingBases: false, objectExists: true, awaitingSpawnEnd: false));
    }
}
