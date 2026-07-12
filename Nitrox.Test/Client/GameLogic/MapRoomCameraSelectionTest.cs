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
}
