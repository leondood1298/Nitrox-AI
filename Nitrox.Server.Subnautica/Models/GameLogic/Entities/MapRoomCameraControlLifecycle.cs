using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
}
