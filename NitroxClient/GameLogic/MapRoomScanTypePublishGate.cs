using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;

namespace NitroxClient.GameLogic;

/// <summary>
///     Decides whether a Scanner Room's available scan types need to be sent to the server again. A snapshot is sent
///     as soon as the set of types or the scan range changes, and otherwise at most once per refresh interval so the
///     server can still pick up in-range resources this client has not loaded.
/// </summary>
internal sealed class MapRoomScanTypePublishGate
{
    internal const float UNCHANGED_REFRESH_SECONDS = 5f;
    private const float RANGE_EPSILON = 0.01f;
    private readonly Dictionary<NitroxId, Published> lastByRoom = [];

    public bool ShouldPublish(NitroxId roomId, IEnumerable<TechType> availableTechTypes, float scanRange, float now)
    {
        if (lastByRoom.TryGetValue(roomId, out Published last) &&
            now - last.Time < UNCHANGED_REFRESH_SECONDS &&
            Math.Abs(last.ScanRange - scanRange) < RANGE_EPSILON &&
            last.TechTypes.SetEquals(availableTechTypes))
        {
            return false;
        }
        lastByRoom[roomId] = new Published([..availableTechTypes], scanRange, now);
        return true;
    }

    private readonly record struct Published(HashSet<TechType> TechTypes, float ScanRange, float Time);
}
