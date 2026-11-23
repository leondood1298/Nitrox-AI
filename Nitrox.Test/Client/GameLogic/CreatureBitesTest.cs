using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class CreatureBitesTest
{
    [TestMethod]
    public void SimulatingPlayersStalkerBitesAndSendsTheBite()
    {
        Assert.IsTrue(CreatureBites.MayBiteHere(bitesShownFromSimulatingPlayer: true, localPlayerSimulatesCreature: true, movedByAnotherPlayer: false));
        Assert.IsTrue(CreatureBites.ShouldSend(bitesShownFromSimulatingPlayer: true, localPlayerSimulatesCreature: true));
    }

    [TestMethod]
    public void CopyOfAnotherPlayersStalkerOnlyShowsTheBitesItIsSent()
    {
        // Biting on its own as well, it would show a bite twice, or one that never happened where the damage is decided
        Assert.IsFalse(CreatureBites.MayBiteHere(bitesShownFromSimulatingPlayer: true, localPlayerSimulatesCreature: false, movedByAnotherPlayer: true));
        Assert.IsFalse(CreatureBites.ShouldSend(bitesShownFromSimulatingPlayer: true, localPlayerSimulatesCreature: false));
    }

    [TestMethod]
    public void StalkerNobodySimulatesKeepsBitingLocally()
    {
        // Nobody would send its bites
        Assert.IsTrue(CreatureBites.MayBiteHere(bitesShownFromSimulatingPlayer: true, localPlayerSimulatesCreature: false, movedByAnotherPlayer: false));
    }

    [TestMethod]
    public void OtherCreaturesBiteAsBeforeAndSendNothing()
    {
        Assert.IsTrue(CreatureBites.MayBiteHere(bitesShownFromSimulatingPlayer: false, localPlayerSimulatesCreature: false, movedByAnotherPlayer: true));
        Assert.IsFalse(CreatureBites.ShouldSend(bitesShownFromSimulatingPlayer: false, localPlayerSimulatesCreature: true));
    }
}
