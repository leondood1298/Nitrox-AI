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

    private const string WATER_PARK_CLASS_ID = "31662630-7cba-4583-8456-2fa1c4cc31aa";

    private static UpdateBase Update(NitroxId ghostId, (NitroxId, NitroxId) transfer, int operationId) =>
        new(BuildingScenarioFixture.BaseId, ghostId, new BaseData(), null, new Dictionary<NitroxId, NitroxBaseFace>(), new Dictionary<NitroxId, NitroxInt3>(),
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
