using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class BleedersTest
{
    [TestMethod]
    public void ABleederAtAPlayersBodyLatchesOn()
    {
        Assert.IsTrue(Bleeders.TouchesBody(0.8f));
        Assert.IsTrue(Bleeders.TouchesBody(Bleeders.TOUCH_DISTANCE));
    }

    [TestMethod]
    public void ABleederSwimmingPastDoesNot()
    {
        Assert.IsFalse(Bleeders.TouchesBody(3f));
    }
}
