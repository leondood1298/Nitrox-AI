using System.Reflection;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;

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

    /// <summary>
    ///     Metadata processors and extractors are registered as single instances and live as long as the program. The
    ///     services of the game logic live as long as one session. A processor that takes such a service in its
    ///     constructor gets one of its own, made outside any session, which no patch and no packet processor talks to:
    ///     what the server sends would be applied to state nothing else reads. They look services up when they need
    ///     them (Resolve) and may only be handed other processors or extractors.
    /// </summary>
    [TestMethod]
    public void ProcessorsAndExtractorsTakeNoSessionServicesInTheirConstructors()
    {
        Type[] types;
        try
        {
            types = typeof(EntityMetadataManager).Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            types = exception.Types.Where(type => type != null).ToArray()!;
        }
        Type[] metadataTypes = types.Where(type => !type.IsAbstract && (typeof(IEntityMetadataProcessor).IsAssignableFrom(type) || typeof(IEntityMetadataExtractor).IsAssignableFrom(type))).ToArray();

        // Upstream's vehicle processors take these two, and upstream's code is left as it is. Nothing else is to follow them.
        Type[] upstreamExceptions = [typeof(IPacketSender), typeof(LiveMixinManager)];

        string[] offenders = metadataTypes.SelectMany(type => type.GetConstructors().SelectMany(constructor => constructor.GetParameters())
                                                                  .Where(parameter => !metadataTypes.Contains(parameter.ParameterType) && !upstreamExceptions.Contains(parameter.ParameterType))
                                                                  .Select(parameter => $"{type.Name}({parameter.ParameterType.Name})"))
                                          .ToArray();

        Assert.IsTrue(metadataTypes.Length > 20, "The processors and extractors were not found.");
        Assert.AreEqual(0, offenders.Length, string.Join(", ", offenders));
    }
}
