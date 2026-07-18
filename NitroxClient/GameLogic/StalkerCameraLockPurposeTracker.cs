using System.Collections.Generic;
using Nitrox.Model.DataStructures;

namespace NitroxClient.GameLogic;

/// <summary>
/// Tracks whether a local Scanner Room camera exclusive lock was acquired for a Stalker grab.
/// Camera control and Stalker AI share the same server lock, so release decisions cannot be made
/// from the lock type alone.
/// </summary>
public sealed class StalkerCameraLockPurposeTracker
{
    private readonly object syncRoot = new();
    private readonly HashSet<NitroxId> acquiredForStalker = [];
    private readonly HashSet<NitroxId> releasesDeferredForCameraControl = [];

    public void RecordAcquisition(NitroxId cameraId, bool cameraControlAlreadyGranted)
    {
        lock (syncRoot)
        {
            releasesDeferredForCameraControl.Remove(cameraId);
            if (cameraControlAlreadyGranted)
            {
                acquiredForStalker.Remove(cameraId);
            }
            else
            {
                acquiredForStalker.Add(cameraId);
            }
        }
    }

    public bool TryConsumeForDowngrade(NitroxId cameraId, bool currentlyExclusive, bool cameraControlProtected)
    {
        lock (syncRoot)
        {
            if (!acquiredForStalker.Remove(cameraId))
            {
                return false;
            }
            if (cameraControlProtected)
            {
                releasesDeferredForCameraControl.Add(cameraId);
                return false;
            }
            releasesDeferredForCameraControl.Remove(cameraId);
            return currentlyExclusive;
        }
    }

    public bool CompleteCameraControlRequest(NitroxId cameraId, bool granted, bool currentlyExclusive)
    {
        lock (syncRoot)
        {
            if (granted)
            {
                acquiredForStalker.Remove(cameraId);
                releasesDeferredForCameraControl.Remove(cameraId);
                return false;
            }
            return releasesDeferredForCameraControl.Remove(cameraId) && currentlyExclusive;
        }
    }

    public void Forget(NitroxId cameraId)
    {
        lock (syncRoot)
        {
            acquiredForStalker.Remove(cameraId);
            releasesDeferredForCameraControl.Remove(cameraId);
        }
    }
}
