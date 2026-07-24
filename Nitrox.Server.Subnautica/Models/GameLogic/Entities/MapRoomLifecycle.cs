using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Nitrox.Model.DataStructures;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <summary>
///     Serializes Scanner Room operations that require the room to remain registered with
///     deconstruction, which removes the room before its camera cleanup is broadcast.
/// </summary>
internal sealed class MapRoomLifecycle
{
    private readonly ConcurrentDictionary<NitroxId, SemaphoreSlim> gates = new();

    public async ValueTask<IDisposable> EnterAsync(NitroxId roomId)
    {
        SemaphoreSlim gate = gates.GetOrAdd(roomId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        return new Releaser(gate);
    }

    public async ValueTask<IReadOnlyList<IDisposable>> EnterManyAsync(IEnumerable<NitroxId> roomIds)
    {
        List<IDisposable> acquired = [];
        try
        {
            foreach (NitroxId roomId in roomIds.Distinct().OrderBy(id => id.ToString(), StringComparer.Ordinal))
            {
                acquired.Add(await EnterAsync(roomId));
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
