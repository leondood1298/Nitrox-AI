using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Serialization;
using Packet = Nitrox.Model.Packets.Packet;

namespace Nitrox.Test.Server.GameLogic;

/// <summary>
///     The fabricator of a Scanner Room is a child entity of the room that is found by its path in the room's game
///     object, like the fabricator of a Cyclops. What it crafts is its metadata, and players take turns on it with a
///     simulation lock on its id.
/// </summary>
[TestClass]
public sealed class ScannerRoomManagerFabricatorTest
{
    private static readonly NitroxId baseId = new("00000000-0000-0000-0000-000000000500");
    private static readonly NitroxId roomId = new("00000000-0000-0000-0000-000000000501");
    private static readonly NitroxId fabricatorId = new("00000000-0000-0000-0000-000000000502");
    private static readonly CrafterMetadata crafting = new(new NitroxTechType("MapRoomHUDChip"), 12.5f, 4f, 1, 0);

    [TestMethod]
    public async Task FabricatorComesWithTheRoomAndGoesToTheOtherPlayersAJoiningPlayerAndTheSave()
    {
        ScannerRoomScenarioFixture scenario = new();

        await BuildRoomWithFabricatorAsync(scenario);

        PathBasedChildEntity fabricator = scenario.EntityRegistry.GetEntityById<PathBasedChildEntity>(fabricatorId).Value;
        MapRoomEntity room = scenario.EntityRegistry.GetEntityById<MapRoomEntity>(roomId).Value;
        Assert.AreEqual(roomId, fabricator.ParentId);
        CollectionAssert.Contains(room.ChildEntities, fabricator);
        // The other players build the room themselves, but its fabricator would have no id in their game.
        MapRoomEntity relayed = (MapRoomEntity)scenario.SentToOthers<UpdateBase>().Single().BuiltPieceEntity;
        Assert.AreEqual(fabricatorId, relayed.ChildEntities.OfType<PathBasedChildEntity>().Single().Id);

        await scenario.UpdateMetadataAsync(scenario.PlayerB, fabricatorId, crafting);
        List<GlobalRootEntity> sync = scenario.Manager.DetachRooms(scenario.WorldEntityManager.GetGlobalRootEntities(true));
        // What the fabricator crafts changes while the copy is read.
        await scenario.UpdateMetadataAsync(scenario.PlayerB, fabricatorId, new CrafterMetadata(NitroxTechType.None, 0f, 0f, 0, 0));

        PathBasedChildEntity synced = sync.Single(entity => entity.Id == baseId).ChildEntities.OfType<MapRoomEntity>().Single().ChildEntities.OfType<PathBasedChildEntity>().Single();
        Assert.AreNotSame(fabricator, synced);
        Assert.AreEqual(("Fabricator/logic", fabricatorId, roomId), (synced.Path, synced.Id, synced.ParentId));
        Assert.AreSame(crafting, synced.Metadata, "A copy is the room at one moment.");

        ServerJsonSerializer serializer = new(NullLogger<ServerJsonSerializer>.Instance);
        using MemoryStream payload = new();
        serializer.Serialize(payload, GlobalRootData.From(sync));
        using MemoryStream input = new(payload.ToArray());
        MapRoomEntity savedRoom = serializer.Deserialize<GlobalRootData>(input).Entities.Single(entity => entity.Id == baseId).ChildEntities.OfType<MapRoomEntity>().Single();
        PathBasedChildEntity saved = savedRoom.ChildEntities.OfType<PathBasedChildEntity>().Single();
        Assert.AreEqual(("Fabricator/logic", fabricatorId, roomId), (saved.Path, saved.Id, saved.ParentId));
        CrafterMetadata savedCrafting = (CrafterMetadata)saved.Metadata;
        Assert.AreEqual((crafting.TechType, crafting.StartTime, crafting.Duration, crafting.Amount), (savedCrafting.TechType, savedCrafting.StartTime, savedCrafting.Duration, savedCrafting.Amount));
        Assert.AreEqual(2, ScannerRoomStateFingerprint.GetCameras(savedRoom).Length, "The cameras in the docks are saved next to it.");
    }

    [TestMethod]
    public async Task TwoPlayersTakeTurnsOnTheFabricator()
    {
        ScannerRoomScenarioFixture scenario = new();
        await BuildRoomWithFabricatorAsync(scenario);

        // A fabricator is no entity of the world, so its lock is simply granted or refused.
        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerA, fabricatorId, SimulationLockType.EXCLUSIVE)).LockAcquired, "The player who starts crafting.");
        Assert.IsFalse((await scenario.RequestOwnershipAsync(scenario.PlayerB, fabricatorId, SimulationLockType.EXCLUSIVE)).LockAcquired, "Nobody else while it crafts.");
        Assert.AreSame(scenario.PlayerA, scenario.Ownership.GetPlayerForLock(fabricatorId));

        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerA, fabricatorId, SimulationLockType.TRANSIENT)).LockAcquired, "The game gives the lock back when the item is done.");
        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerB, fabricatorId, SimulationLockType.EXCLUSIVE)).LockAcquired, "Then it is the other player's turn.");

        Assert.IsTrue(scenario.Ownership.TryGetLock(fabricatorId, out SimulationOwnershipData.PlayerLock heldLock));
        Assert.AreEqual((scenario.PlayerB, SimulationLockType.EXCLUSIVE), (heldLock.Player, heldLock.LockType));
        SimulatedEntity announced = scenario.SentToOthers<SimulationOwnershipChange>().Last().Entities.Single();
        Assert.AreEqual((fabricatorId, scenario.PlayerB.SessionId, SimulationLockType.EXCLUSIVE), (announced.Id, announced.SessionId, announced.LockType));
    }

    [TestMethod]
    public async Task DeconstructedRoomTakesItsFabricatorAndTheLockOnItAlong()
    {
        ScannerRoomScenarioFixture scenario = new();
        await BuildRoomWithFabricatorAsync(scenario);
        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerA, fabricatorId, SimulationLockType.EXCLUSIVE)).LockAcquired);
        MapRoomEntity room = scenario.EntityRegistry.GetEntityById<MapRoomEntity>(roomId).Value;
        Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(roomId).HasValue);

        IReadOnlyList<Packet> sent = await scenario.CleanupAsync(room);

        Assert.IsFalse(scenario.EntityRegistry.GetEntityById(fabricatorId).HasValue);
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(fabricatorId), "A lock on something that is gone would never be given back.");
        Assert.IsTrue(sent.OfType<DropSimulationOwnership>().Any(drop => drop.EntityIds.Contains(fabricatorId)));
    }

    /// <summary>
    ///     Builds a room the way the builder's client reports it: with its two cameras and its fabricator as children.
    /// </summary>
    private static Task<IReadOnlyList<Packet>> BuildRoomWithFabricatorAsync(ScannerRoomScenarioFixture scenario)
    {
        MapRoomEntity room = new(roomId, baseId, new NitroxInt3(2, 0, 2));
        room.ChildEntities.Add(ScannerRoomScenarioFixture.CreateCamera(new NitroxId("00000000-0000-0000-0000-000000000511"), roomId, new MapRoomCameraMetadata(dockingIndex: 0)));
        room.ChildEntities.Add(ScannerRoomScenarioFixture.CreateCamera(new NitroxId("00000000-0000-0000-0000-000000000512"), roomId, new MapRoomCameraMetadata(dockingIndex: 1)));
        room.ChildEntities.Add(new PathBasedChildEntity("Fabricator/logic", fabricatorId, null, null, roomId, []));
        return scenario.BuildRoomAsync(scenario.PlayerA, room);
    }
}
