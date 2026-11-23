using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using NSubstitute;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class AITest
{
    private readonly AI ai = new(Substitute.For<IPacketSender>());

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
