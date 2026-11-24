using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class SharedCreatureSpawnsTest
{
    [TestMethod]
    public void TheNearestPlayerMakesTheCreature()
    {
        Assert.IsTrue(SharedCreatureSpawns.IsNearest(10f, [25f, 80f]));
    }

    [TestMethod]
    public void AFartherPlayerLeavesItToTheNearest()
    {
        Assert.IsFalse(SharedCreatureSpawns.IsNearest(30f, [25f, 80f]));
    }

    [TestMethod]
    public void AloneThePlayerMakesIt()
    {
        Assert.IsTrue(SharedCreatureSpawns.IsNearest(500f, []));
    }

    [TestMethod]
    public void AWarpedInCreatureWarpsOutAtTheSameGameTimeOnAnotherGame()
    {
        // Made 30 s before its end on a game started 100 s ago, at game time 5000
        double warpOutGameTime = SharedCreatureSpawns.GetWarpOutGameTime(130f, 100f, 5000d);
        Assert.AreEqual(5030d, warpOutGameTime);

        // Another game, started 7 s ago, gets it 10 game seconds later
        Assert.AreEqual(27f, SharedCreatureSpawns.GetWarpOutLocalTime(warpOutGameTime, 5010d, 7f), 0.001f);
    }

    [TestMethod]
    public void AWarpedInCreatureWhoseTimePassedWarpsOutAtOnce()
    {
        // Loaded again after a restart, long after its end
        Assert.IsTrue(SharedCreatureSpawns.GetWarpOutLocalTime(5030d, 9000d, 3f) < 3f);
    }

    [TestMethod]
    public void TheJuvenileHasTheSameIdInEveryGame()
    {
        // Tiny differences in where a game has the spawner must not give another id
        Assert.AreEqual(SharedCreatureSpawns.GetJuvenileId(312.2f, -1405.9f, 80.1f), SharedCreatureSpawns.GetJuvenileId(312.3f, -1406.1f, 79.9f));
        Assert.AreNotEqual(SharedCreatureSpawns.GetJuvenileId(312f, -1406f, 80f), SharedCreatureSpawns.GetJuvenileId(0f, -1406f, 80f));
    }
}
