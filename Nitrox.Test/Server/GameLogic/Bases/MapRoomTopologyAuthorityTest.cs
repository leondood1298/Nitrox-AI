using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Test.Server.GameLogic.Bases;

[TestClass]
public sealed class MapRoomTopologyAuthorityTest
{
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

        Assert.IsTrue(MapRoomTopologyAuthority.Validate(entityRegistry, update));
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

        Assert.IsFalse(MapRoomTopologyAuthority.Validate(entityRegistry, update));
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

        Assert.IsFalse(MapRoomTopologyAuthority.Validate(entityRegistry, update));
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

        Assert.IsFalse(MapRoomTopologyAuthority.Validate(entityRegistry, update));
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

        Assert.IsFalse(MapRoomTopologyAuthority.Validate(entityRegistry, update));
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

        Assert.IsFalse(MapRoomTopologyAuthority.Validate(entityRegistry, update));
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

        Assert.IsFalse(MapRoomTopologyAuthority.Validate(entityRegistry, update));
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

        Assert.IsTrue(MapRoomTopologyAuthority.Validate(entityRegistry, update));
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

        Assert.IsTrue(MapRoomTopologyAuthority.Validate(entityRegistry, update));
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

        Assert.IsFalse(MapRoomTopologyAuthority.Validate(entityRegistry, update));
    }

    [TestMethod]
    public void AllowsExistingBaseChild()
    {
        NitroxId baseId = new();
        Assert.IsTrue(MapRoomTopologyAuthority.IsAllowedParent(baseId, baseId, (null, null)));
    }

    [TestMethod]
    public void AllowsChildFromBaseBeingMergedIntoTarget()
    {
        NitroxId source = new();
        NitroxId target = new();
        Assert.IsTrue(MapRoomTopologyAuthority.IsAllowedParent(source, target, (source, target)));
    }

    [TestMethod]
    public void RejectsUnrelatedOrReverseTransferParent()
    {
        NitroxId source = new();
        NitroxId target = new();
        NitroxId unrelated = new();

        Assert.IsFalse(MapRoomTopologyAuthority.IsAllowedParent(unrelated, target, (source, target)));
        Assert.IsFalse(MapRoomTopologyAuthority.IsAllowedParent(source, target, (target, source)));
        Assert.IsFalse(MapRoomTopologyAuthority.IsAllowedParent(null, target, (source, target)));
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
}
