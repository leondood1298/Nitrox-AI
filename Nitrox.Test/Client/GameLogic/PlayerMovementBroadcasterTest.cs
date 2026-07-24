using Microsoft.VisualStudio.TestTools.UnitTesting;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class PlayerMovementBroadcasterTest
{
    [DataTestMethod]
    [DataRow(false, false, false, (int)PlayerMovementBroadcastMode.SUPPRESSED)]
    [DataRow(true, false, false, (int)PlayerMovementBroadcastMode.NORMAL)]
    [DataRow(false, true, false, (int)PlayerMovementBroadcastMode.NORMAL)]
    [DataRow(false, false, true, (int)PlayerMovementBroadcastMode.SCANNER_CAMERA_ANCHOR)]
    [DataRow(true, false, true, (int)PlayerMovementBroadcastMode.SCANNER_CAMERA_ANCHOR)]
    public void BroadcastModePinsOnlyScannerCameraControl(bool mainCameraEnabled, bool cyclopsCameraActive,
        bool scannerCameraActive, int expected)
    {
        Assert.AreEqual((PlayerMovementBroadcastMode)expected, PlayerMovementBroadcaster.GetBroadcastMode(
            mainCameraEnabled, cyclopsCameraActive, scannerCameraActive));
    }

    [TestMethod]
    public void ScannerCameraAnchorForcesZeroVelocity()
    {
        Vector3 currentVelocity = new(3f, -2f, 7f);

        Assert.AreEqual(Vector3.zero, PlayerMovementBroadcaster.GetBroadcastVelocity(true, currentVelocity));
        Assert.AreEqual(currentVelocity, PlayerMovementBroadcaster.GetBroadcastVelocity(false, currentVelocity));
    }

    [DataTestMethod]
    [DataRow(false, false, false, false, (int)ScannerAnchorTransition.NONE)]
    [DataRow(false, true, true, false, (int)ScannerAnchorTransition.ENTER)]
    [DataRow(true, true, true, true, (int)ScannerAnchorTransition.SWITCH)]
    [DataRow(true, true, false, true, (int)ScannerAnchorTransition.IDENTIFIED)]
    [DataRow(true, true, false, false, (int)ScannerAnchorTransition.NONE)]
    [DataRow(true, false, true, true, (int)ScannerAnchorTransition.EXIT)]
    public void ScannerAnchorDiagnosticsTrackCameraSwitchAndExit(bool wasActive, bool active,
        bool cameraChanged, bool idChanged, int expected)
    {
        Assert.AreEqual((ScannerAnchorTransition)expected, PlayerMovementBroadcaster.GetScannerAnchorTransition(
            wasActive, active, cameraChanged, idChanged));
    }
}
