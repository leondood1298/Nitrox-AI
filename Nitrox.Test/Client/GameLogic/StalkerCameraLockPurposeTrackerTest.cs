using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class StalkerCameraLockPurposeTrackerTest
{
    private readonly NitroxId cameraId = new("00000000-0000-0000-0000-000000000225");

    [TestMethod]
    public void UntrackedExclusiveLockIsNeverDowngraded()
    {
        StalkerCameraLockPurposeTracker tracker = new();

        Assert.IsFalse(tracker.TryConsumeForDowngrade(cameraId, true, false));
    }

    [TestMethod]
    public void StalkerAcquisitionIsConsumedExactlyOnce()
    {
        StalkerCameraLockPurposeTracker tracker = new();
        tracker.RecordAcquisition(cameraId, cameraControlAlreadyGranted: false);

        Assert.IsTrue(tracker.TryConsumeForDowngrade(cameraId, true, false));
        Assert.IsFalse(tracker.TryConsumeForDowngrade(cameraId, true, false));
    }

    [TestMethod]
    public void LostExclusiveLockStillConsumesStalkerPurposeWithoutDowngrade()
    {
        StalkerCameraLockPurposeTracker tracker = new();
        tracker.RecordAcquisition(cameraId, cameraControlAlreadyGranted: false);

        Assert.IsFalse(tracker.TryConsumeForDowngrade(cameraId, false, false));
        Assert.IsFalse(tracker.TryConsumeForDowngrade(cameraId, true, false));
    }

    [TestMethod]
    public void PendingCameraControlDefersReleaseUntilRequestIsDenied()
    {
        StalkerCameraLockPurposeTracker tracker = new();
        tracker.RecordAcquisition(cameraId, cameraControlAlreadyGranted: false);

        Assert.IsFalse(tracker.TryConsumeForDowngrade(cameraId, true, cameraControlProtected: true));
        Assert.IsTrue(tracker.CompleteCameraControlRequest(cameraId, granted: false, currentlyExclusive: true));
        Assert.IsFalse(tracker.CompleteCameraControlRequest(cameraId, granted: false, currentlyExclusive: true));
    }

    [TestMethod]
    public void GrantedCameraControlCancelsDeferredStalkerRelease()
    {
        StalkerCameraLockPurposeTracker tracker = new();
        tracker.RecordAcquisition(cameraId, cameraControlAlreadyGranted: false);
        Assert.IsFalse(tracker.TryConsumeForDowngrade(cameraId, true, cameraControlProtected: true));

        Assert.IsFalse(tracker.CompleteCameraControlRequest(cameraId, granted: true, currentlyExclusive: true));
        Assert.IsFalse(tracker.CompleteCameraControlRequest(cameraId, granted: false, currentlyExclusive: true));
        Assert.IsFalse(tracker.TryConsumeForDowngrade(cameraId, true, false));
    }

    [TestMethod]
    public void ExistingCameraControlSupersedesStalkerPurpose()
    {
        StalkerCameraLockPurposeTracker tracker = new();
        tracker.RecordAcquisition(cameraId, cameraControlAlreadyGranted: false);
        tracker.RecordAcquisition(cameraId, cameraControlAlreadyGranted: true);

        Assert.IsFalse(tracker.TryConsumeForDowngrade(cameraId, true, false));
    }
}
