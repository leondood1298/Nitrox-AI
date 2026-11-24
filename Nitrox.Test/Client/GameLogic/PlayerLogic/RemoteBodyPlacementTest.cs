using NitroxClient.GameLogic.PlayerLogic;
using UnityEngine;

namespace Nitrox.Test.Client.GameLogic.PlayerLogic;

[TestClass]
public sealed class RemoteBodyPlacementTest
{
    /// <summary>
    ///     A quarter turn to the right about the vertical axis; Quaternion.Euler is native to Unity and cannot run here.
    /// </summary>
    private static readonly Quaternion facingEast = new(0, 0.70710678f, 0, 0.70710678f);

    [TestMethod]
    public void PlayerInOpenWaterStandsWhereTheServerKeepsThem()
    {
        Quaternion facing = facingEast;

        (Vector3 position, Quaternion? rotation) = RemoteBodyPlacement.Place(new Vector3(10, -5, 20), facing, false, Vector3.zero, Quaternion.identity, false);

        position.Should().Be(new Vector3(10, -5, 20));
        rotation.Should().Be(facing);
    }

    [TestMethod]
    public void PlayerInATurnedCyclopsStandsInItWhereverItWent()
    {
        Quaternion cyclopsFacingEast = facingEast;

        (Vector3 position, Quaternion? rotation) = RemoteBodyPlacement.Place(new Vector3(0, 0, 2), Quaternion.identity, true, new Vector3(100, -20, 100), cyclopsFacingEast, false);

        (position - new Vector3(102, -20, 100)).magnitude.Should().BeLessThan(0.001f);
        Quaternion.Angle(rotation!.Value, cyclopsFacingEast).Should().BeLessThan(0.01f);
    }

    [TestMethod]
    public void PlayerInABaseIsPlacedButNotTurned()
    {
        (Vector3 position, Quaternion? rotation) = RemoteBodyPlacement.Place(new Vector3(1, 0, 0), Quaternion.identity, true, new Vector3(5, 5, 5), Quaternion.identity, true);

        position.Should().Be(new Vector3(6, 5, 5));
        rotation.Should().BeNull();
    }
}
