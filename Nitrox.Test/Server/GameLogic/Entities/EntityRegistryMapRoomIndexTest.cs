using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Test.Server.GameLogic.Entities;

[TestClass]
public sealed class EntityRegistryMapRoomIndexTest
{
    [TestMethod]
    public void ScannerRoomLookupFollowsEveryWayAnEntityIsAddedReplacedOrRemoved()
    {
        EntityRegistry registry = new(NullLogger<EntityRegistry>.Instance);
        MapRoomEntity added = CreateRoom();
        MapRoomEntity upserted = CreateRoom();
        MapRoomEntity nested = CreateRoom();
        ModuleEntity module = ModuleEntity.MakeEmpty();
        module.Id = new NitroxId();

        registry.AddEntity(added);
        registry.AddEntity(module);
        registry.AddOrUpdate(upserted);
        registry.AddEntitiesIgnoringDuplicate([nested]);

        CollectionAssert.AreEquivalent(new[] { added, upserted, nested }, registry.GetEntities<MapRoomEntity>());

        MapRoomEntity replacement = new(upserted.Id, new NitroxId(), new NitroxInt3(1, 2, 3));
        registry.AddOrUpdate(replacement);
        registry.RemoveEntity(added.Id);

        List<MapRoomEntity> rooms = registry.GetEntities<MapRoomEntity>();
        CollectionAssert.AreEquivalent(new[] { replacement, nested }, rooms);
        Assert.IsFalse(rooms.Any(room => ReferenceEquals(room, upserted)), "A replaced room must not stay in the lookup.");
    }

    [TestMethod]
    public void ScannerRoomLookupMatchesAFullRegistryFilter()
    {
        EntityRegistry registry = new(NullLogger<EntityRegistry>.Instance);
        for (int index = 0; index < 50; index++)
        {
            ModuleEntity module = ModuleEntity.MakeEmpty();
            module.Id = new NitroxId();
            registry.AddEntity(module);
            if (index % 10 == 0)
            {
                registry.AddEntity(CreateRoom());
            }
        }

        List<MapRoomEntity> expected = registry.GetAllEntities().OfType<MapRoomEntity>().ToList();

        Assert.AreEqual(5, expected.Count);
        CollectionAssert.AreEquivalent(expected, registry.GetEntities<MapRoomEntity>());
    }

    private static MapRoomEntity CreateRoom() => new(new NitroxId(), new NitroxId(), new NitroxInt3(0, 0, 0));
}
