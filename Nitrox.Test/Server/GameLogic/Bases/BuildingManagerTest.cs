using Nitrox.Model.DataStructures;
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
