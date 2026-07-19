using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <summary>
///     Serializes control acquisition with destructive camera lifecycle transitions. The gate is
///     held until the release and every related ownership/drop/lifecycle packet have been queued,
///     preventing an old cleanup packet from overtaking a newly granted controller.
/// </summary>
internal sealed class MapRoomCameraControlLifecycle
{
    private readonly ConcurrentDictionary<NitroxId, SemaphoreSlim> gates = new();
    private readonly ConcurrentDictionary<NitroxId, PreviewAcquisition> previewAcquisitions = new();
    private long previewRevision;

    public async ValueTask<IDisposable> EnterAsync(NitroxId cameraId)
    {
        SemaphoreSlim gate = gates.GetOrAdd(cameraId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        return new Releaser(gate);
    }

    internal bool IsKnown(NitroxId cameraId) => gates.ContainsKey(cameraId);

    internal void RememberKnown(NitroxId cameraId) =>
        gates.GetOrAdd(cameraId, static _ => new SemaphoreSlim(1, 1));

    internal void RememberMany(IEnumerable<NitroxId> cameraIds)
    {
        foreach (NitroxId cameraId in cameraIds)
        {
            RememberKnown(cameraId);
        }
    }

    /// <summary>
    ///     Opens exactly one preview-publication opportunity for a successful exclusive control acquisition.
    ///     Callers hold the camera lifecycle gate while changing this state.
    /// </summary>
    internal bool BeginPreviewAcquisition(NitroxId cameraId, SessionId controllerSessionId)
    {
        if (previewAcquisitions.TryGetValue(cameraId, out PreviewAcquisition existing) &&
            existing.ControllerSessionId == controllerSessionId)
        {
            // A replayed/idempotent control-acquire response must not reset an already consumed preview.
            return false;
        }
        previewAcquisitions[cameraId] = new PreviewAcquisition(controllerSessionId);
        return true;
    }

    internal void EndPreviewAcquisition(NitroxId cameraId, SessionId controllerSessionId)
    {
        if (previewAcquisitions.TryGetValue(cameraId, out PreviewAcquisition current) &&
            current.ControllerSessionId == controllerSessionId)
        {
            previewAcquisitions.TryRemove(cameraId, out _);
        }
    }

    internal bool TryConsumePreviewAcquisition(NitroxId cameraId, SessionId controllerSessionId)
    {
        if (!previewAcquisitions.TryGetValue(cameraId, out PreviewAcquisition current) ||
            current.ControllerSessionId != controllerSessionId)
        {
            return false;
        }
        lock (current)
        {
            if (current.Consumed)
            {
                return false;
            }
            current.Consumed = true;
            return true;
        }
    }

    internal long NextPreviewRevision() => Interlocked.Increment(ref previewRevision);

    public async ValueTask<IReadOnlyList<IDisposable>> EnterManyAsync(IEnumerable<NitroxId> cameraIds)
    {
        List<IDisposable> acquired = [];
        try
        {
            foreach (NitroxId cameraId in cameraIds.Distinct().OrderBy(id => id.ToString(), StringComparer.Ordinal))
            {
                acquired.Add(await EnterAsync(cameraId));
            }
            return acquired;
        }
        catch
        {
            ReleaseReverse(acquired);
            throw;
        }
    }

    public static void ReleaseReverse(IReadOnlyList<IDisposable> acquired)
    {
        for (int index = acquired.Count - 1; index >= 0; index--)
        {
            acquired[index].Dispose();
        }
    }

    private sealed class Releaser(SemaphoreSlim gate) : IDisposable
    {
        private SemaphoreSlim? gate = gate;

        public void Dispose() => Interlocked.Exchange(ref gate, null)?.Release();
    }

    private sealed class PreviewAcquisition(SessionId controllerSessionId)
    {
        internal SessionId ControllerSessionId { get; } = controllerSessionId;
        internal bool Consumed { get; set; }
    }
}
