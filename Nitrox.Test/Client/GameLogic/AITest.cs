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

    [TestMethod]
    public void BittenPlayersOwnCopyOfASyncedLeviathanDoesTheBiting()
    {
        Assert.IsTrue(ai.IsAttackReplayedByVictim(typeof(ReaperLeviathan)));
    }

    [TestMethod]
    [DataRow(typeof(Stalker))]
    [DataRow(typeof(SandShark))]
    public void StalkerAndSandSharkBitesStayReportedByTheirSimulatingPlayer(Type creatureType)
    {
        // Their aim and attack are replayed everywhere, but a bite counts where the simulating player saw it land
        Assert.IsFalse(ai.IsAttackReplayedByVictim(creatureType));
    }

    [TestMethod]
    [DataRow(typeof(Stalker))]
    [DataRow(typeof(SandShark))]
    public void StalkerAndSandSharkBitesAreShownFromTheirSimulatingPlayer(Type creatureType)
    {
        Assert.IsTrue(ai.AreBitesShownFromSimulatingPlayer(creatureType));
    }

    [TestMethod]
    [DataRow(typeof(ReaperLeviathan))]
    [DataRow(typeof(CrabSnake))]
    public void OtherCreaturesShowTheirOwnBites(Type creatureType)
    {
        Assert.IsFalse(ai.AreBitesShownFromSimulatingPlayer(creatureType));
    }

    [TestMethod]
    public void UnsyncedCreatureBitesAreReported()
    {
        Assert.IsFalse(ai.IsAttackReplayedByVictim(typeof(CrabSnake)));
    }
}
