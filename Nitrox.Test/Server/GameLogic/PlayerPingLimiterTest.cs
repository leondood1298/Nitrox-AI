using Nitrox.Model.Core;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

[TestClass]
public sealed class PlayerPingLimiterTest
{
    [TestMethod]
    public void ABurstIsRelayedAndTheNextPingIsRefused()
    {
        PlayerPingLimiter limiter = new(() => TimeSpan.Zero);

        for (int i = 0; i < PlayerPingLimiter.BURST; i++)
        {
            Assert.IsTrue(limiter.TryPing((SessionId)1));
        }
        Assert.IsFalse(limiter.TryPing((SessionId)1));
    }

    [TestMethod]
    public void AnotherPlayerIsNotLimitedByTheFirst()
    {
        PlayerPingLimiter limiter = new(() => TimeSpan.Zero);
        for (int i = 0; i < PlayerPingLimiter.BURST; i++)
        {
            limiter.TryPing((SessionId)1);
        }

        Assert.IsTrue(limiter.TryPing((SessionId)2));
    }

    [TestMethod]
    public void PingsAreAllowedAgainOnceTheWindowHasPassed()
    {
        TimeSpan now = TimeSpan.Zero;
        PlayerPingLimiter limiter = new(() => now);
        for (int i = 0; i < PlayerPingLimiter.BURST; i++)
        {
            limiter.TryPing((SessionId)1);
        }

        now = PlayerPingLimiter.WINDOW;

        Assert.IsTrue(limiter.TryPing((SessionId)1));
    }
}
