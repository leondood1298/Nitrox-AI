using Microsoft.VisualStudio.TestTools.UnitTesting;
using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class MapRoomCameraSelectionTest
{
    [DataTestMethod]
    [DataRow(false, false, false, true)]
    [DataRow(true, false, false, false)]
    [DataRow(false, false, true, false)]
    [DataRow(false, true, false, true)]
    [DataRow(false, true, true, true)]
    public void SelectionReflectsControllerState(bool pending, bool local, bool remote, bool expected)
    {
        Assert.AreEqual(expected, MapRoomCameras.CanSelectForControl(pending, local, remote));
    }

    [DataTestMethod]
    [DataRow(0f, 100f, 1f, 1f)]
    [DataRow(99.5f, 100f, 1f, 0.5f)]
    [DataRow(100f, 100f, 1f, 0f)]
    [DataRow(0f, 0f, 1f, 0f)]
    [DataRow(0f, 100f, 0f, 0f)]
    public void DockChargeIsBoundedPerSecond(float current, float capacity, float deltaTime, float expected)
    {
        Assert.AreEqual(expected, MapRoomCameras.CalculateDockCharge(current, capacity, deltaTime), 0.0001f);
    }
}
