using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class CreatureBitesTest
{
    [TestMethod]
    public void SimulatingPlayersCreatureBites()
    {
        Assert.IsTrue(CreatureBites.MayBiteHere(localPlayerSimulatesCreature: true, movedByAnotherPlayer: false));
    }

    [TestMethod]
    public void CopyOfAnotherPlayersCreatureOnlyShowsTheBitesItIsSent()
    {
        // Biting on its own as well, it would show a bite twice, or one that never happened where the damage is decided
        Assert.IsFalse(CreatureBites.MayBiteHere(localPlayerSimulatesCreature: false, movedByAnotherPlayer: true));
    }

    [TestMethod]
    public void CreatureNobodySimulatesKeepsBitingLocally()
    {
        // Nobody would send its bites
        Assert.IsTrue(CreatureBites.MayBiteHere(localPlayerSimulatesCreature: false, movedByAnotherPlayer: false));
    }
}
