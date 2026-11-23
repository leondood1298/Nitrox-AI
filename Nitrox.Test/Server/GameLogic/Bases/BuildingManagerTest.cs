using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;

namespace Nitrox.Test.Server.GameLogic.Bases;

[TestClass]
public sealed class BuildingManagerTest
{
    private static readonly NitroxId reactorId = new("00000000-0000-0000-0000-000000000501");

    /// <summary>
    ///     A player whose deconstruction of a reactor was refused still has a ghost with the reactor's id and goes on
    ///     unbuilding it. That must not remove the server's reactor, which everybody else still has.
    /// </summary>
    [TestMethod]
    public void ConstructedAmountForABuiltPieceIsRefused()
    {
        BuildingScenarioFixture scenario = new();
        scenario.AddInteriorPiece(reactorId, "BaseBioReactor", BuildingScenarioFixture.Item("00000000-0000-0000-0000-000000000601", "Peeper"));

        bool accepted = scenario.BuildingManager.ModifyConstructedAmount(scenario.PlayerB, new ModifyConstructedAmount(reactorId, 0f));

        Assert.IsFalse(accepted);
        Assert.IsTrue(scenario.EntityRegistry.TryGetEntityById(reactorId, out InteriorPieceEntity reactor));
        Assert.HasCount(1, reactor.ChildEntities);
        Assert.HasCount(1, scenario.SentTo<BuildingDesyncWarning>(scenario.PlayerB), "The player has to learn that its base is out of sync.");
    }

    [TestMethod]
    public void ConstructedAmountForABaseIsRefused()
    {
        BuildingScenarioFixture scenario = new();

        Assert.IsFalse(scenario.BuildingManager.ModifyConstructedAmount(scenario.PlayerB, new ModifyConstructedAmount(BuildingScenarioFixture.BaseId, 0f)));
        Assert.IsTrue(scenario.EntityRegistry.TryGetEntityById(BuildingScenarioFixture.BaseId, out BuildEntity _));
    }

    /// <summary>
    ///     A transfer that names water parks of another base is a leftover from another build; it must not empty a planter.
    /// </summary>
    [TestMethod]
    public void TransferNamingAnotherBaseIsIgnored()
    {
        BuildingScenarioFixture scenario = new();
        NitroxId otherBaseId = new("00000000-0000-0000-0000-000000000510");
        BuildEntity otherBase = BuildEntity.MakeEmpty();
        otherBase.Id = otherBaseId;
        scenario.Add(otherBase);
        NitroxId waterParkId = new("00000000-0000-0000-0000-000000000520");
        InteriorPieceEntity waterPark = InteriorPieceEntity.MakeEmpty();
        waterPark.Id = waterParkId;
        waterPark.ParentId = otherBaseId;
        waterPark.ClassId = WATER_PARK_CLASS_ID;
        PlanterEntity planter = new(waterParkId.Increment(), waterParkId);
        planter.ChildEntities.Add(BuildingScenarioFixture.Item("00000000-0000-0000-0000-000000000530", "BulboTreePiece", planter.Id));
        waterPark.ChildEntities.Add(planter);
        scenario.Add(waterPark);
        NitroxId ghostId = new("00000000-0000-0000-0000-000000000540");
        scenario.AddGhost(ghostId);

        Assert.IsTrue(scenario.BuildingManager.UpdateBase(scenario.PlayerA, Update(ghostId, (waterParkId, waterParkId), 1), out _));

        Assert.HasCount(1, scenario.EntityRegistry.GetEntityById<PlanterEntity>(planter.Id).Value.ChildEntities);
    }

    [TestMethod]
    public void TransferWithinTheBaseEmptiesTheMergedPlanter()
    {
        BuildingScenarioFixture scenario = new();
        NitroxId waterParkId = new("00000000-0000-0000-0000-000000000520");
        InteriorPieceEntity waterPark = scenario.AddInteriorPiece(waterParkId, "BaseWaterPark");
        waterPark.ClassId = WATER_PARK_CLASS_ID;
        PlanterEntity planter = new(waterParkId.Increment(), waterParkId);
        planter.ChildEntities.Add(BuildingScenarioFixture.Item("00000000-0000-0000-0000-000000000530", "BulboTreePiece", planter.Id));
        scenario.EntityRegistry.AddOrUpdate(planter);
        NitroxId ghostId = new("00000000-0000-0000-0000-000000000540");
        scenario.AddGhost(ghostId);
        UpdateBase update = Update(ghostId, (waterParkId, waterParkId), 1);
        update.UpdatedChildren[waterParkId] = default;

        Assert.IsTrue(scenario.BuildingManager.UpdateBase(scenario.PlayerA, update, out _));

        Assert.IsEmpty(scenario.EntityRegistry.GetEntityById<PlanterEntity>(planter.Id).Value.ChildEntities);
    }

    /// <summary>
    ///     A partly deconstructed reactor keeps what is in it, in the game as on the server: a joiner, a restart and the
    ///     player who finishes it again all get the rods back.
    /// </summary>
    [TestMethod]
    public void PartlyDeconstructedReactorKeepsItsContents()
    {
        BuildingScenarioFixture scenario = new();
        InventoryItemEntity rod = BuildingScenarioFixture.Item("00000000-0000-0000-0000-000000000601", "ReactorRod");
        scenario.AddInteriorPiece(reactorId, "BaseNuclearReactor", rod);

        Assert.IsTrue(scenario.BuildingManager.ReplacePieceByGhost(scenario.PlayerA, Deconstructed(reactorId, 1), out _, out _));

        Assert.IsTrue(scenario.EntityRegistry.TryGetEntityById(reactorId, out GhostEntity ghost));
        Assert.AreSame(rod, ghost.ChildEntities.Single());
        Assert.AreEqual(reactorId, scenario.EntityRegistry.GetEntityById(rod.Id).Value.ParentId);

        // Finished again by a player who has nothing in its copy of the module
        InteriorPieceEntity rebuilt = InteriorPieceEntity.MakeEmpty();
        rebuilt.Id = reactorId;
        rebuilt.ParentId = BuildingScenarioFixture.BaseId;
        Assert.IsTrue(scenario.BuildingManager.UpdateBase(scenario.PlayerB, Update(reactorId, default, 2, rebuilt), out _));

        Assert.IsTrue(scenario.EntityRegistry.TryGetEntityById(reactorId, out InteriorPieceEntity reactor));
        Assert.AreSame(rod, reactor.ChildEntities.Single());
        Assert.AreEqual(reactorId, scenario.EntityRegistry.GetEntityById(rod.Id).Value.ParentId);
    }

    /// <summary>
    ///     Two players finish pieces of one base at the same moment: the later one is refused and told which base, so that
    ///     its game resyncs that base by itself.
    /// </summary>
    [TestMethod]
    public void LateOperationNamesItsBase()
    {
        BuildingScenarioFixture scenario = new();
        NitroxId ghostA = new("00000000-0000-0000-0000-000000000551");
        NitroxId ghostB = new("00000000-0000-0000-0000-000000000552");
        scenario.AddGhost(ghostA);
        scenario.AddGhost(ghostB);

        Assert.IsTrue(scenario.BuildingManager.UpdateBase(scenario.PlayerA, Update(ghostA, default, 1), out _));
        Assert.IsFalse(scenario.BuildingManager.UpdateBase(scenario.PlayerB, Update(ghostB, default, 1), out _));

        BuildingDesyncWarning warning = scenario.SentTo<BuildingDesyncWarning>(scenario.PlayerB).Single();
        Assert.AreEqual(BuildingScenarioFixture.BaseId, warning.BaseId);
        Assert.AreEqual(1, warning.Operations[BuildingScenarioFixture.BaseId]);
    }

    /// <summary>
    ///     A removes the last room while B attaches a corridor to it. The corridor came first: the base must not become a
    ///     ghost of one room and lose it.
    /// </summary>
    [TestMethod]
    public void LastPieceRemovalAfterAnotherPlayersBuildIsRefused()
    {
        BuildingScenarioFixture scenario = new();
        NitroxId corridorGhost = new("00000000-0000-0000-0000-000000000561");
        scenario.AddGhost(corridorGhost);
        Assert.IsTrue(scenario.BuildingManager.UpdateBase(scenario.PlayerB, Update(corridorGhost, default, 1), out _));

        bool accepted = scenario.BuildingManager.ReplaceBaseByGhost(scenario.PlayerA,
            new BaseDeconstructed(BuildingScenarioFixture.BaseId, BaseGhost(), 1));

        Assert.IsFalse(accepted);
        Assert.IsTrue(scenario.EntityRegistry.TryGetEntityById(BuildingScenarioFixture.BaseId, out BuildEntity _));
        Assert.AreEqual(BuildingScenarioFixture.BaseId, scenario.SentTo<BuildingDesyncWarning>(scenario.PlayerA).Single().BaseId);
    }

    [TestMethod]
    public void LastPieceRemovalInOrderReplacesTheBase()
    {
        BuildingScenarioFixture scenario = new();

        Assert.IsTrue(scenario.BuildingManager.ReplaceBaseByGhost(scenario.PlayerA,
            new BaseDeconstructed(BuildingScenarioFixture.BaseId, BaseGhost(), 1)));

        Assert.IsTrue(scenario.EntityRegistry.TryGetEntityById(BuildingScenarioFixture.BaseId, out GhostEntity _));
    }

    private static GhostEntity BaseGhost()
    {
        GhostEntity ghost = BuildingScenarioFixture.Ghost(BuildingScenarioFixture.BaseId);
        ghost.ParentId = null;
        return ghost;
    }

    private static PieceDeconstructed Deconstructed(NitroxId pieceId, int operationId) =>
        new(BuildingScenarioFixture.BaseId, pieceId, default, BuildingScenarioFixture.Ghost(pieceId), new BaseData(), operationId);

    /// <summary>
    ///     A builder whose game failed to spawn a containment does not list it; the fish in it must not be removed for that.
    /// </summary>
    [TestMethod]
    public void UnlistedWaterParkWithFishIsKept()
    {
        BuildingScenarioFixture scenario = new();
        NitroxId waterParkId = new("00000000-0000-0000-0000-000000000520");
        InteriorPieceEntity waterPark = scenario.AddInteriorPiece(waterParkId, "BaseWaterPark", BuildingScenarioFixture.Item("00000000-0000-0000-0000-000000000531", "Peeper"));
        waterPark.ClassId = WATER_PARK_CLASS_ID;
        NitroxId ghostId = new("00000000-0000-0000-0000-000000000540");
        scenario.AddGhost(ghostId);

        Assert.IsTrue(scenario.BuildingManager.UpdateBase(scenario.PlayerA, Update(ghostId, default, 1), out _));

        Assert.IsTrue(scenario.EntityRegistry.TryGetEntityById(waterParkId, out InteriorPieceEntity _));
    }

    [TestMethod]
    public void MergedWaterParkIsRemovedAndItsFishMove()
    {
        BuildingScenarioFixture scenario = new();
        NitroxId upperId = new("00000000-0000-0000-0000-000000000521");
        NitroxId lowerId = new("00000000-0000-0000-0000-000000000522");
        InventoryItemEntity fish = BuildingScenarioFixture.Item("00000000-0000-0000-0000-000000000531", "Peeper");
        scenario.AddInteriorPiece(upperId, "BaseWaterPark", fish).ClassId = WATER_PARK_CLASS_ID;
        scenario.AddInteriorPiece(lowerId, "BaseWaterPark").ClassId = WATER_PARK_CLASS_ID;
        NitroxId ghostId = new("00000000-0000-0000-0000-000000000540");
        scenario.AddGhost(ghostId);
        UpdateBase update = Update(ghostId, (upperId, lowerId), 1);
        update.UpdatedChildren[lowerId] = default;

        Assert.IsTrue(scenario.BuildingManager.UpdateBase(scenario.PlayerA, update, out _));

        Assert.IsFalse(scenario.EntityRegistry.GetEntityById(upperId).HasValue);
        Assert.AreEqual(lowerId, scenario.EntityRegistry.GetEntityById(fish.Id).Value.ParentId);
    }

    private const string WATER_PARK_CLASS_ID = "31662630-7cba-4583-8456-2fa1c4cc31aa";

    private static UpdateBase Update(NitroxId ghostId, (NitroxId, NitroxId) transfer, int operationId, Entity? builtPiece = null) =>
        new(BuildingScenarioFixture.BaseId, ghostId, new BaseData(), builtPiece, new Dictionary<NitroxId, NitroxBaseFace>(), new Dictionary<NitroxId, NitroxInt3>(),
            new Dictionary<NitroxId, NitroxInt3>(), transfer, operationId);

    [TestMethod]
    public void GhostIsRemovedWhenUnbuilt()
    {
        BuildingScenarioFixture scenario = new();
        NitroxId ghostId = new("00000000-0000-0000-0000-000000000502");
        scenario.AddGhost(ghostId);

        Assert.IsTrue(scenario.BuildingManager.ModifyConstructedAmount(scenario.PlayerA, new ModifyConstructedAmount(ghostId, 0.5f)));
        Assert.AreEqual(0.5f, scenario.EntityRegistry.GetEntityById<GhostEntity>(ghostId).Value.ConstructedAmount);
        Assert.IsTrue(scenario.BuildingManager.ModifyConstructedAmount(scenario.PlayerA, new ModifyConstructedAmount(ghostId, 0f)));
        Assert.IsFalse(scenario.EntityRegistry.GetEntityById(ghostId).HasValue);
    }
}
