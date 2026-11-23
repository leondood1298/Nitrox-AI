using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using NSubstitute;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class AITest
{
    private readonly AI ai = new(Substitute.For<IPacketSender>(), new SimulationOwnership(Substitute.For<IMultiplayerSession>(), Substitute.For<IPacketSender>()));

    [TestMethod]
    public void ACreatureNobodySharesRunsEverything()
    {
        Assert.IsTrue(AI.MayRunActionHere(false, false, false, true));
        Assert.IsTrue(AI.MayRunActionHere(false, false, false, false));
    }

    [TestMethod]
    public void TheSimulatingPlayerRunsEverything()
    {
        Assert.IsTrue(AI.MayRunActionHere(true, true, false, true));
        Assert.IsTrue(AI.MayRunActionHere(true, true, false, false));
    }

    [TestMethod]
    public void OnlyTheSimulatingPlayerOrAnUnsharedCreatureStartsAnActionOutOfTurn()
    {
        Assert.IsTrue(AI.MayChooseActionHere(false, false));
        Assert.IsTrue(AI.MayChooseActionHere(true, true));
        Assert.IsFalse(AI.MayChooseActionHere(true, false));
    }

    [TestMethod]
    public void ACopyOnlyStartsAndStopsTheActionsItReplays()
    {
        Assert.IsTrue(AI.MayRunActionHere(true, false, true, false));
        Assert.IsFalse(AI.MayRunActionHere(true, false, true, true));
        Assert.IsFalse(AI.MayRunActionHere(true, false, false, false));
        Assert.IsFalse(AI.MayRunActionHere(true, false, false, true));
    }

    [TestMethod]
    [DataRow(typeof(AttackLastTarget))]
    [DataRow(typeof(RangedAttackLastTarget))]
    [DataRow(typeof(AttackCyclops))]
    [DataRow(typeof(Poop))]
    [DataRow(typeof(SwimToMeat))]
    [DataRow(typeof(SwimToHeroPeeper))]
    public void PoseActionsAreReplayed(Type actionType)
    {
        Assert.IsTrue(ai.IsReplayedAction(actionType));
    }

    [TestMethod]
    [DataRow(typeof(SwimRandom))]
    [DataRow(typeof(FleeOnDamage))]
    [DataRow(typeof(CollectShiny))]
    [DataRow(typeof(BurryInSand))]
    public void ActionsThatMoveOrChangeTheCreatureAreNotReplayed(Type actionType)
    {
        Assert.IsFalse(ai.IsReplayedAction(actionType));
    }
}
