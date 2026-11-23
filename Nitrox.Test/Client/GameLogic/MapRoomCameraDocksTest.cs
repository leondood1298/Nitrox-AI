using NitroxClient.GameLogic;
using UnityEngine;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class MapRoomCameraDocksTest
{
    /// <summary>
    ///     A dock is named to the server by its index in its room. Every client works that index out by itself, so
    ///     every client has to put the docks of a room in the same order.
    /// </summary>
    [TestMethod]
    [DataRow(0f, 0f, 0f, 1f, 0f, 0f, -1)]
    [DataRow(1f, -5f, -5f, 0f, 9f, 9f, 1)]
    [DataRow(0f, 0f, 0f, 0.005f, 1f, 0f, -1)]
    [DataRow(0.005f, 1f, 0f, 0f, 0f, 0f, 1)]
    [DataRow(0f, 2f, 0f, 0.005f, 2.005f, -3f, 1)]
    [DataRow(1f, 2f, 3f, 1f, 2f, 3f, 0)]
    public void DocksAreOrderedByPositionWithATolerance(float ax, float ay, float az, float bx, float by, float bz, int expected)
    {
        Assert.AreEqual(expected, Math.Sign(MapRoomCameraDocks.CompareWorldPosition(new Vector3(ax, ay, az), new Vector3(bx, by, bz))));
    }
}
