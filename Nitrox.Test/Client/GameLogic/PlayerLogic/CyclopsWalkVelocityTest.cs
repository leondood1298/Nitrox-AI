using NitroxClient.GameLogic.PlayerLogic;
using UnityEngine;

namespace Nitrox.Test.Client.GameLogic.PlayerLogic;

[TestClass]
public sealed class CyclopsWalkVelocityTest
{
    [TestMethod]
    public void WalkingSpeedDoesNotDependOnHowOftenTheWalkerSends()
    {
        CyclopsWalkVelocity fast = new();
        CyclopsWalkVelocity slow = new();
        // 2 metres per second forward, sent at 144 and at 30 packets per second
        for (int i = 0; i <= 144; i++)
        {
            fast.OnPacket(new Vector3(0, 0, i * 2f / 144f), i / 144f);
        }
        for (int i = 0; i <= 30; i++)
        {
            slow.OnPacket(new Vector3(0, 0, i * 2f / 30f), i / 30f);
        }

        fast.GetVelocity(1f).z.Should().BeApproximately(2f, 0.01f);
        slow.GetVelocity(1f).z.Should().BeApproximately(2f, 0.01f);
    }

    [TestMethod]
    public void PacketsThatArriveInOneFrameAreMeasuredOverALongerSpan()
    {
        CyclopsWalkVelocity walk = new();
        walk.OnPacket(Vector3.zero, 0f);
        walk.OnPacket(new Vector3(0.1f, 0, 0), 0.05f);
        walk.OnPacket(new Vector3(0.2f, 0, 0), 0.05f);
        walk.OnPacket(new Vector3(0.3f, 0, 0), 0.1f);

        walk.GetVelocity(0.1f).x.Should().BeApproximately(2f, 0.01f);
    }

    [TestMethod]
    public void PlayerWhoStopsSendingIsShownStandingStill()
    {
        CyclopsWalkVelocity walk = new();
        walk.OnPacket(Vector3.zero, 0f);
        walk.OnPacket(new Vector3(0, 0, 0.1f), 0.05f);

        walk.GetVelocity(0.05f + CyclopsWalkVelocity.EXPIRY_SECONDS + 0.01f).Should().Be(Vector3.zero);
    }

    [TestMethod]
    public void ResyncOfAStillPlayerShowsNoMovement()
    {
        CyclopsWalkVelocity walk = new();
        walk.OnPacket(new Vector3(1, 0, 1), 0f);
        walk.OnPacket(new Vector3(1, 0, 1), 2f);

        walk.GetVelocity(2f).Should().Be(Vector3.zero);
    }

    [TestMethod]
    public void TeleportIsNotShownAsASprint()
    {
        CyclopsWalkVelocity walk = new();
        walk.OnPacket(Vector3.zero, 0f);
        walk.OnPacket(new Vector3(0, 0, 30f), 0.02f);

        walk.GetVelocity(0.02f).magnitude.Should().BeApproximately(CyclopsWalkVelocity.MAXIMUM_SPEED, 0.01f);
    }
}
