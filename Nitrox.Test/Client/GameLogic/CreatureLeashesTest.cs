using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class CreatureLeashesTest
{
    [TestMethod]
    public void ALeashThatHardlyMovedIsNotSentAgain()
    {
        // A Skyray or a following Cuddlefish moves its leash every update
        Assert.IsFalse(CreatureLeashes.LeashMovedEnough(0.5f));
    }

    [TestMethod]
    public void AMovedLeashIsSent()
    {
        // A creature that fled (FleeOnDamage breaks its leash) or a Stalker that found a nest
        Assert.IsTrue(CreatureLeashes.LeashMovedEnough(CreatureLeashes.LEASH_RESEND_DISTANCE));
        Assert.IsTrue(CreatureLeashes.LeashMovedEnough(40f));
    }
}
