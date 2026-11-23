using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Bases;
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
///     Rooms coming into the world and leaving it: which rooms a base update may name, and what comes and goes with a
///     room besides its cameras. Its fabricator is a child entity of the room that is found by its path in the room's
///     game object, like the fabricator of a Cyclops. What it crafts is its metadata, and players take turns on it
///     with a simulation lock on its id.
/// </summary>
[TestClass]
public sealed class ScannerRoomManagerLifecycleTest
{
    private static readonly NitroxId fabricatorBaseId = new("00000000-0000-0000-0000-000000000500");
    private static readonly NitroxId fabricatorRoomId = new("00000000-0000-0000-0000-000000000501");
    private static readonly NitroxId fabricatorId = new("00000000-0000-0000-0000-000000000502");
    private static readonly CrafterMetadata crafting = new(new NitroxTechType("MapRoomHUDChip"), 12.5f, 4f, 1, 0);

    [TestMethod]
    public void AllowsNewlyBuiltRoomBeforeItReplacesGhostInRegistry()
    {
        NitroxId baseId = new();
        NitroxId ghostId = new();
        NitroxInt3 cell = new(1, 2, 3);
        MapRoomEntity builtRoom = new(ghostId, baseId, cell);
        EntityRegistry entityRegistry = new(NullLogger<EntityRegistry>.Instance);
        entityRegistry.AddEntity(CreateGhost(ghostId, baseId));
        UpdateBase update = CreateUpdate(baseId, ghostId, builtRoom, new Dictionary<NitroxId, NitroxInt3> { [ghostId] = cell });

        Assert.IsTrue(ScannerRoomManager.IsValidRoomTopology(entityRegistry, update));
    }

    [TestMethod]
    public void RejectsNewRoomWhoseIdDoesNotMatchFormerGhost()
    {
        NitroxId baseId = new();
        NitroxId ghostId = new();
        NitroxId roomId = new();
        NitroxInt3 cell = new(1, 2, 3);
        MapRoomEntity builtRoom = new(roomId, baseId, cell);
        EntityRegistry entityRegistry = new(NullLogger<EntityRegistry>.Instance);
        entityRegistry.AddEntity(CreateGhost(ghostId, baseId));
        UpdateBase update = CreateUpdate(baseId, ghostId, builtRoom, new Dictionary<NitroxId, NitroxInt3> { [roomId] = cell });

        Assert.IsFalse(ScannerRoomManager.IsValidRoomTopology(entityRegistry, update));
    }

    [TestMethod]
    public void RejectsNewRoomWhoseCellDiffersFromTopology()
    {
        NitroxId baseId = new();
        NitroxId ghostId = new();
        MapRoomEntity builtRoom = new(ghostId, baseId, new NitroxInt3(1, 2, 3));
        EntityRegistry entityRegistry = new(NullLogger<EntityRegistry>.Instance);
        entityRegistry.AddEntity(CreateGhost(ghostId, baseId));
        UpdateBase update = CreateUpdate(baseId, ghostId, builtRoom, new Dictionary<NitroxId, NitroxInt3> { [ghostId] = new(4, 5, 6) });

        Assert.IsFalse(ScannerRoomManager.IsValidRoomTopology(entityRegistry, update));
    }

    [TestMethod]
    public void RejectsNewRoomWhoseParentIsNotPartOfUpdate()
    {
        NitroxId baseId = new();
        NitroxId ghostId = new();
        NitroxId unrelatedBaseId = new();
        NitroxInt3 cell = new(1, 2, 3);
        MapRoomEntity builtRoom = new(ghostId, unrelatedBaseId, cell);
        EntityRegistry entityRegistry = new(NullLogger<EntityRegistry>.Instance);
        entityRegistry.AddEntity(CreateGhost(ghostId, baseId));
        UpdateBase update = CreateUpdate(baseId, ghostId, builtRoom, new Dictionary<NitroxId, NitroxInt3> { [ghostId] = cell });

        Assert.IsFalse(ScannerRoomManager.IsValidRoomTopology(entityRegistry, update));
    }

    [TestMethod]
    public void RejectsNewRoomWhoseGhostParentIsNotTargetBase()
    {
        NitroxId sourceBaseId = new();
        NitroxId targetBaseId = new();
        NitroxId ghostId = new();
        NitroxInt3 cell = new(1, 2, 3);
        MapRoomEntity builtRoom = new(ghostId, targetBaseId, cell);
        EntityRegistry entityRegistry = new(NullLogger<EntityRegistry>.Instance);
        entityRegistry.AddEntity(CreateGhost(ghostId, sourceBaseId));
        UpdateBase update = CreateUpdate(targetBaseId, ghostId, builtRoom,
                                         new Dictionary<NitroxId, NitroxInt3> { [ghostId] = cell },
                                         (sourceBaseId, targetBaseId));

        Assert.IsFalse(ScannerRoomManager.IsValidRoomTopology(entityRegistry, update));
    }

    [TestMethod]
    public void RejectsNewRoomMissingFromTopology()
    {
        NitroxId baseId = new();
        NitroxId ghostId = new();
        MapRoomEntity builtRoom = new(ghostId, baseId, new NitroxInt3(1, 2, 3));
        EntityRegistry entityRegistry = new(NullLogger<EntityRegistry>.Instance);
        entityRegistry.AddEntity(CreateGhost(ghostId, baseId));
        UpdateBase update = CreateUpdate(baseId, ghostId, builtRoom, new Dictionary<NitroxId, NitroxInt3>());

        Assert.IsFalse(ScannerRoomManager.IsValidRoomTopology(entityRegistry, update));
    }

    [TestMethod]
    public void RejectsNewRoomBuiltFromNonScannerGhost()
    {
        NitroxId baseId = new();
        NitroxId ghostId = new();
        NitroxInt3 cell = new(1, 2, 3);
        MapRoomEntity builtRoom = new(ghostId, baseId, cell);
        EntityRegistry entityRegistry = new(NullLogger<EntityRegistry>.Instance);
        entityRegistry.AddEntity(CreateGhost(ghostId, baseId, "BaseCorridor"));
        UpdateBase update = CreateUpdate(baseId, ghostId, builtRoom, new Dictionary<NitroxId, NitroxInt3> { [ghostId] = cell });

        Assert.IsFalse(ScannerRoomManager.IsValidRoomTopology(entityRegistry, update));
    }

    [TestMethod]
    public void AllowsRegisteredRoomUpdate()
    {
        NitroxId baseId = new();
        NitroxId roomId = new();
        NitroxInt3 cell = new(1, 2, 3);
        EntityRegistry entityRegistry = new(NullLogger<EntityRegistry>.Instance);
        entityRegistry.AddEntity(new MapRoomEntity(roomId, baseId, cell));
        UpdateBase update = CreateUpdate(baseId, new NitroxId(), null, new Dictionary<NitroxId, NitroxInt3> { [roomId] = cell });

        Assert.IsTrue(ScannerRoomManager.IsValidRoomTopology(entityRegistry, update));
    }

    [TestMethod]
    public void AllowsRegisteredRoomTransferredIntoTargetBase()
    {
        NitroxId sourceBaseId = new();
        NitroxId targetBaseId = new();
        NitroxId roomId = new();
        NitroxInt3 cell = new(1, 2, 3);
        EntityRegistry entityRegistry = new(NullLogger<EntityRegistry>.Instance);
        entityRegistry.AddEntity(new MapRoomEntity(roomId, sourceBaseId, cell));
        UpdateBase update = CreateUpdate(targetBaseId, new NitroxId(), null,
                                         new Dictionary<NitroxId, NitroxInt3> { [roomId] = cell },
                                         (sourceBaseId, targetBaseId));

        Assert.IsTrue(ScannerRoomManager.IsValidRoomTopology(entityRegistry, update));
    }

    [TestMethod]
    public void RejectsDuplicateRegisteredRoomCells()
    {
        NitroxId baseId = new();
        NitroxId firstRoomId = new();
        NitroxId secondRoomId = new();
        NitroxInt3 cell = new(1, 2, 3);
        EntityRegistry entityRegistry = new(NullLogger<EntityRegistry>.Instance);
        entityRegistry.AddEntity(new MapRoomEntity(firstRoomId, baseId, cell));
        entityRegistry.AddEntity(new MapRoomEntity(secondRoomId, baseId, cell));
        UpdateBase update = CreateUpdate(baseId, new NitroxId(), null, new Dictionary<NitroxId, NitroxInt3>
        {
            [firstRoomId] = cell,
            [secondRoomId] = cell
        });

        Assert.IsFalse(ScannerRoomManager.IsValidRoomTopology(entityRegistry, update));
    }

    [TestMethod]
    public void AllowsExistingBaseChild()
    {
        NitroxId baseId = new();
        Assert.IsTrue(ScannerRoomManager.IsAllowedRoomParent(baseId, baseId, (null, null)));
    }

    [TestMethod]
    public void AllowsChildFromBaseBeingMergedIntoTarget()
    {
        NitroxId source = new();
        NitroxId target = new();
        Assert.IsTrue(ScannerRoomManager.IsAllowedRoomParent(source, target, (source, target)));
    }

    [TestMethod]
    public void RejectsUnrelatedOrReverseTransferParent()
    {
        NitroxId source = new();
        NitroxId target = new();
        NitroxId unrelated = new();

        Assert.IsFalse(ScannerRoomManager.IsAllowedRoomParent(unrelated, target, (source, target)));
        Assert.IsFalse(ScannerRoomManager.IsAllowedRoomParent(source, target, (target, source)));
        Assert.IsFalse(ScannerRoomManager.IsAllowedRoomParent(null, target, (source, target)));
    }

    [TestMethod]
    public async Task FabricatorComesWithTheRoomAndGoesToTheOtherPlayersAJoiningPlayerAndTheSave()
    {
        ScannerRoomScenarioFixture scenario = new();

        await BuildRoomWithFabricatorAsync(scenario);

        PathBasedChildEntity fabricator = scenario.EntityRegistry.GetEntityById<PathBasedChildEntity>(fabricatorId).Value;
        MapRoomEntity room = scenario.EntityRegistry.GetEntityById<MapRoomEntity>(fabricatorRoomId).Value;
        Assert.AreEqual(fabricatorRoomId, fabricator.ParentId);
        CollectionAssert.Contains(room.ChildEntities, fabricator);
        // The other players build the room themselves, but its fabricator would have no id in their game.
        MapRoomEntity relayed = (MapRoomEntity)scenario.SentToOthers<UpdateBase>().Single().BuiltPieceEntity;
        Assert.AreEqual(fabricatorId, relayed.ChildEntities.OfType<PathBasedChildEntity>().Single().Id);

        await scenario.UpdateMetadataAsync(scenario.PlayerB, fabricatorId, crafting);
        List<GlobalRootEntity> sync = scenario.Manager.DetachRooms(scenario.WorldEntityManager.GetGlobalRootEntities(true));
        // What the fabricator crafts changes while the copy is read.
        await scenario.UpdateMetadataAsync(scenario.PlayerB, fabricatorId, new CrafterMetadata(NitroxTechType.None, 0f, 0f, 0, 0));

        PathBasedChildEntity synced = sync.Single(entity => entity.Id == fabricatorBaseId).ChildEntities.OfType<MapRoomEntity>().Single().ChildEntities.OfType<PathBasedChildEntity>().Single();
        Assert.AreNotSame(fabricator, synced);
        Assert.AreEqual(("Fabricator/logic", fabricatorId, fabricatorRoomId), (synced.Path, synced.Id, synced.ParentId));
        Assert.AreSame(crafting, synced.Metadata, "A copy is the room at one moment.");

        ServerJsonSerializer serializer = new(NullLogger<ServerJsonSerializer>.Instance);
        using MemoryStream payload = new();
        serializer.Serialize(payload, GlobalRootData.From(sync));
        using MemoryStream input = new(payload.ToArray());
        MapRoomEntity savedRoom = serializer.Deserialize<GlobalRootData>(input).Entities.Single(entity => entity.Id == fabricatorBaseId).ChildEntities.OfType<MapRoomEntity>().Single();
        PathBasedChildEntity saved = savedRoom.ChildEntities.OfType<PathBasedChildEntity>().Single();
        Assert.AreEqual(("Fabricator/logic", fabricatorId, fabricatorRoomId), (saved.Path, saved.Id, saved.ParentId));
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
        MapRoomEntity room = scenario.EntityRegistry.GetEntityById<MapRoomEntity>(fabricatorRoomId).Value;
        Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(fabricatorRoomId).HasValue);

        IReadOnlyList<Packet> sent = await scenario.CleanupAsync(room);

        Assert.IsFalse(scenario.EntityRegistry.GetEntityById(fabricatorId).HasValue);
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(fabricatorId), "A lock on something that is gone would never be given back.");
        Assert.IsTrue(sent.OfType<DropSimulationOwnership>().Any(drop => drop.EntityIds.Contains(fabricatorId)));
    }

    private static GhostEntity CreateGhost(NitroxId ghostId, NitroxId parentId, string techType = "BaseMapRoom")
    {
        GhostEntity ghost = GhostEntity.MakeEmpty();
        ghost.Id = ghostId;
        ghost.ParentId = parentId;
        ghost.TechType = new NitroxTechType(techType);
        return ghost;
    }

    private static UpdateBase CreateUpdate(NitroxId baseId, NitroxId formerGhostId, MapRoomEntity? builtRoom,
                                           Dictionary<NitroxId, NitroxInt3> updatedMapRooms,
                                           (NitroxId From, NitroxId To) childrenTransfer = default)
    {
        return new UpdateBase(baseId, formerGhostId, null, builtRoom,
                              new Dictionary<NitroxId, NitroxBaseFace>(),
                              new Dictionary<NitroxId, NitroxInt3>(),
                              updatedMapRooms, childrenTransfer, 1);
    }

    /// <summary>
    ///     Builds a room the way the builder's client reports it: with its two cameras and its fabricator as children.
    /// </summary>
    private static Task<IReadOnlyList<Packet>> BuildRoomWithFabricatorAsync(ScannerRoomScenarioFixture scenario)
    {
        MapRoomEntity room = new(fabricatorRoomId, fabricatorBaseId, new NitroxInt3(2, 0, 2));
        room.ChildEntities.Add(ScannerRoomScenarioFixture.CreateCamera(new NitroxId("00000000-0000-0000-0000-000000000511"), fabricatorRoomId, new MapRoomCameraMetadata(dockingIndex: 0)));
        room.ChildEntities.Add(ScannerRoomScenarioFixture.CreateCamera(new NitroxId("00000000-0000-0000-0000-000000000512"), fabricatorRoomId, new MapRoomCameraMetadata(dockingIndex: 1)));
        room.ChildEntities.Add(new PathBasedChildEntity("Fabricator/logic", fabricatorId, null, null, fabricatorRoomId, []));
        return scenario.BuildRoomAsync(scenario.PlayerA, room);
    }
}
