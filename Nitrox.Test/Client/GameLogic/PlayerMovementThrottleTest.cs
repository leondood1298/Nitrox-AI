using Nitrox.Model.DataStructures.Unity;
using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

/// <summary>
///     A moving player is sent every frame; a player standing still once, then every few seconds.
/// </summary>
[TestClass]
public sealed class PlayerMovementThrottleTest
{
    private static readonly NitroxQuaternion facing = NitroxQuaternion.Identity;

    [TestMethod]
    public void AMovingPlayerIsSentEveryFrame()
    {
        PlayerMovementThrottle throttle = new();

        for (int frame = 0; frame < 10; frame++)
        {
            throttle.ShouldSend(new NitroxVector3(frame * 0.1f, 0, 0), facing, facing, 6f, frame / 60f).Should().BeTrue();
        }
    }

    [TestMethod]
    public void AStillPlayerIsSentOnceThenOnlyToResync()
    {
        PlayerMovementThrottle throttle = new();
        NitroxVector3 here = new(5, -3, 2);

        throttle.ShouldSend(here, facing, facing, 0f, 0f).Should().BeTrue("the first packet is always sent");
        throttle.ShouldSend(here, facing, facing, 0f, 0.016f).Should().BeTrue("the others are told the player stopped");
        throttle.ShouldSend(here, facing, facing, 0f, 0.032f).Should().BeFalse();
        throttle.ShouldSend(here, facing, facing, 0f, 1f).Should().BeFalse();
        throttle.ShouldSend(here, facing, facing, 0f, 0.016f + PlayerMovementThrottle.RESYNC_PERIOD).Should().BeTrue("a lost packet is made up for");
    }

    [TestMethod]
    public void LookingAroundWhileStandingIsSent()
    {
        PlayerMovementThrottle throttle = new();
        NitroxVector3 here = new(5, -3, 2);
        throttle.ShouldSend(here, facing, facing, 0f, 0f);
        throttle.ShouldSend(here, facing, facing, 0f, 0.016f);

        NitroxQuaternion lookingUp = NitroxQuaternion.FromEuler(-20, 0, 0);

        throttle.ShouldSend(here, facing, lookingUp, 0f, 0.032f).Should().BeTrue();
    }

    [TestMethod]
    public void StartingToMoveAgainIsSentAtOnce()
    {
        PlayerMovementThrottle throttle = new();
        NitroxVector3 here = new(5, -3, 2);
        throttle.ShouldSend(here, facing, facing, 0f, 0f);
        throttle.ShouldSend(here, facing, facing, 0f, 0.016f);
        throttle.ShouldSend(here, facing, facing, 0f, 0.5f);

        throttle.ShouldSend(new NitroxVector3(5.05f, -3, 2), facing, facing, 3f, 0.516f).Should().BeTrue();
    }

    [TestMethod]
    public void AtAHighFrameRateAMovingPlayerIsSentAtMostSixtyTimesASecond()
    {
        PlayerMovementThrottle throttle = new();
        int sent = 0;

        for (int frame = 0; frame < 144; frame++)
        {
            if (throttle.ShouldSend(new NitroxVector3(frame * 0.05f, 0, 0), facing, facing, 6f, frame / 144f))
            {
                sent++;
            }
        }

        sent.Should().BeInRange(50, PlayerMovementThrottle.MAX_SENDS_PER_SECOND);
    }

    [TestMethod]
    public void AtSixtyFramesASecondEveryFrameIsSentEvenWhenFramesWobble()
    {
        PlayerMovementThrottle throttle = new();
        float time = 0;

        for (int frame = 0; frame < 120; frame++)
        {
            throttle.ShouldSend(new NitroxVector3(frame * 0.1f, 0, 0), facing, facing, 6f, time).Should().BeTrue($"frame {frame} at {time}");
            // 60 frames a second on average, each a millisecond early or late
            time += frame % 2 == 0 ? 0.0157f : 0.0176333f;
        }
    }

    [TestMethod]
    public void AStopHeldBackByTheLimitIsSentOnTheNextFrameAllowed()
    {
        PlayerMovementThrottle throttle = new();
        NitroxVector3 here = new(5, -3, 2);
        throttle.ShouldSend(new NitroxVector3(4, -3, 2), facing, facing, 3f, 0f);

        throttle.ShouldSend(here, facing, facing, 0f, 0.005f).Should().BeFalse("too soon after the last packet");
        throttle.ShouldSend(here, facing, facing, 0f, 0.017f).Should().BeTrue("the stop still has to reach the others");
    }

    [TestMethod]
    public void AfterAResetTheNextCallSendsEvenStandingStill()
    {
        PlayerMovementThrottle throttle = new();
        NitroxVector3 here = new(5, -3, 2);
        throttle.ShouldSend(here, facing, facing, 0f, 0f);
        throttle.ShouldSend(here, facing, facing, 0f, 0.02f);
        throttle.ShouldSend(here, facing, facing, 0f, 0.5f).Should().BeFalse();

        throttle.Reset();

        throttle.ShouldSend(here, facing, facing, 0f, 0.52f).Should().BeTrue();
    }

    [TestMethod]
    public void TheAngleBetweenRotationsIsInDegrees()
    {
        PlayerMovementThrottle.AngleBetween(facing, NitroxQuaternion.FromEuler(0, 90, 0)).Should().BeApproximately(90f, 0.1f);
        PlayerMovementThrottle.AngleBetween(facing, facing).Should().BeApproximately(0f, 0.1f);
    }
}
