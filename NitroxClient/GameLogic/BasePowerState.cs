using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxClient.GameLogic;

public sealed class BasePowerState
{
    private readonly Dictionary<NitroxId, PowerSourceMetadata> canonicalBySource = [];
    private readonly Dictionary<NitroxId, long> nextSequenceBySource = [];
    private readonly HashSet<NitroxId> handedBackSources = [];
    private readonly Dictionary<NitroxId, Broadcast> lastBroadcastBySource = [];

    /// <summary>
    ///     What this client last sent for a source it simulates.
    /// </summary>
    /// <param name="Time">Time.realtimeSinceStartup when it was sent.</param>
    public readonly record struct Broadcast(float Time, float Power, float FuelConsumed);

    public Broadcast? GetLastBroadcast(NitroxId sourceId) => lastBroadcastBySource.TryGetValue(sourceId, out Broadcast broadcast) ? broadcast : null;

    public void RecordBroadcast(NitroxId sourceId, Broadcast broadcast) => lastBroadcastBySource[sourceId] = broadcast;

    public BasePowerSourceUpdate CreateUpdate(NitroxId sourceId, BasePowerSourceType sourceType, float power, float fuelConsumed = 0f)
    {
        long sequence = nextSequenceBySource.TryGetValue(sourceId, out long current) && current < long.MaxValue ? current + 1 : 1;
        nextSequenceBySource[sourceId] = sequence;
        return new BasePowerSourceUpdate(sourceId, sourceType, power, sequence, fuelConsumed: fuelConsumed);
    }

    public bool TryApply(NitroxId sourceId, PowerSourceMetadata requested, out PowerSourceMetadata accepted)
    {
        if (canonicalBySource.TryGetValue(sourceId, out PowerSourceMetadata current) && requested.Revision < current.Revision)
        {
            accepted = current;
            return false;
        }
        canonicalBySource[sourceId] = requested;
        accepted = requested;
        return true;
    }

    /// <summary>
    ///     Returns the state a source should show after receiving <paramref name="incoming" />: the incoming state, or the
    ///     newer one already known when a live update overtook it (e.g. a spawn snapshot arriving after a broadcast).
    /// </summary>
    public PowerSourceMetadata Reconcile(NitroxId sourceId, PowerSourceMetadata incoming)
    {
        TryApply(sourceId, incoming, out PowerSourceMetadata newest);
        return newest;
    }

    public bool TryGet(NitroxId sourceId, out PowerSourceMetadata metadata) => canonicalBySource.TryGetValue(sourceId, out metadata);

    /// <summary>
    ///     Drops everything known about a source that no longer exists, so that nothing kept for it can be mistaken
    ///     for the state of a later entity.
    /// </summary>
    public void Forget(NitroxId sourceId)
    {
        canonicalBySource.Remove(sourceId);
        nextSequenceBySource.Remove(sourceId);
        handedBackSources.Remove(sourceId);
        lastBroadcastBySource.Remove(sourceId);
    }

    /// <summary>
    ///     Decides what to do with energy another player took from a source the server says this client simulates. A
    ///     hand-back is decided at most once per source.
    /// </summary>
    /// <param name="simulatedLocally">False when the simulation moved on while the request travelled.</param>
    /// <param name="finishedLoading">While loading, a missing object is still about to spawn.</param>
    public ForwardedDraw DecideForwardedDraw(NitroxId sourceId, bool simulatedLocally, bool sourceObjectExists, bool finishedLoading)
    {
        if (!simulatedLocally)
        {
            return ForwardedDraw.IGNORE;
        }
        if (sourceObjectExists)
        {
            return ForwardedDraw.APPLY;
        }
        // Only once per source: if it comes back because nobody else can simulate it either, handing it back again
        // would only pass it around.
        if (!finishedLoading || !handedBackSources.Add(sourceId))
        {
            return ForwardedDraw.IGNORE;
        }
        return ForwardedDraw.HAND_BACK;
    }
}

public enum ForwardedDraw
{
    /// <summary>
    ///     Nothing to do here: someone else simulates the source by now, or this client cannot do better.
    /// </summary>
    IGNORE,

    /// <summary>
    ///     Take the energy from the source.
    /// </summary>
    APPLY,

    /// <summary>
    ///     The source does not exist on this client: give its simulation back so that someone who has it takes over.
    /// </summary>
    HAND_BACK
}
