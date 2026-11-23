using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class CreatureAttacksOnPlayersTest
{
    [TestMethod]
    public void BiteByASimulatedCreatureOnAnotherPlayerIsReported()
    {
        Assert.IsTrue(CreatureAttacksOnPlayers.ShouldReport(victimIsRemotePlayer: true, localPlayerSimulatesCreature: true));
    }

    [TestMethod]
    public void OnlyTheSimulatingPlayerReportsABite()
    {
        // Every game has a copy of the creature; if each reported what its copy touched, a bite would count several times.
        Assert.IsFalse(CreatureAttacksOnPlayers.ShouldReport(victimIsRemotePlayer: true, localPlayerSimulatesCreature: false));
    }

    [TestMethod]
    public void HitsOnAnythingElseAreNotReported()
    {
        Assert.IsFalse(CreatureAttacksOnPlayers.ShouldReport(victimIsRemotePlayer: false, localPlayerSimulatesCreature: true));
    }

    [TestMethod]
    public void LocalCopyOfAnotherPlayersCreatureDoesNotBiteASecondTime()
    {
        Assert.IsTrue(CreatureAttacksOnPlayers.ShouldLeaveToSimulatingPlayer(victimIsLocalPlayer: true, localPlayerSimulatesCreature: false, movedByAnotherPlayer: true,
                                                                             applyingReportedAttack: false));
    }

    [TestMethod]
    public void ReportedBiteIsApplied()
    {
        Assert.IsFalse(CreatureAttacksOnPlayers.ShouldLeaveToSimulatingPlayer(victimIsLocalPlayer: true, localPlayerSimulatesCreature: false, movedByAnotherPlayer: true,
                                                                              applyingReportedAttack: true));
    }

    [TestMethod]
    public void OwnSimulatedCreatureBitesItsPlayerDirectly()
    {
        Assert.IsFalse(CreatureAttacksOnPlayers.ShouldLeaveToSimulatingPlayer(victimIsLocalPlayer: true, localPlayerSimulatesCreature: true, movedByAnotherPlayer: false,
                                                                              applyingReportedAttack: false));
    }

    [TestMethod]
    public void CreatureNobodySimulatesKeepsBitingLocally()
    {
        // Nobody would report its bites, so skipping them here would make the player immune to it.
        Assert.IsFalse(CreatureAttacksOnPlayers.ShouldLeaveToSimulatingPlayer(victimIsLocalPlayer: true, localPlayerSimulatesCreature: false, movedByAnotherPlayer: false,
                                                                              applyingReportedAttack: false));
    }

    [TestMethod]
    public void DamageToAnythingButTheLocalPlayerIsNotSkipped()
    {
        Assert.IsFalse(CreatureAttacksOnPlayers.ShouldLeaveToSimulatingPlayer(victimIsLocalPlayer: false, localPlayerSimulatesCreature: false, movedByAnotherPlayer: true,
                                                                              applyingReportedAttack: false));
    }
}
