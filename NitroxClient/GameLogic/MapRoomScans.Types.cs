using System;
using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxClient.GameLogic;

/// <summary>
///     The types of resources a room's screen offers to scan for. The simulating player's game works them out from
///     what it has loaded and reports them; the server adds the types of what else is in range.
/// </summary>
public sealed partial class MapRoomScans
{
    /// <summary>
    ///     How long the same list of types is not reported again. The server is still sent it now and then, because
    ///     what it adds changes with the world, not with this client's game.
    /// </summary>
    internal const float UNCHANGED_TYPES_REFRESH_SECONDS = 5f;

    private const float RANGE_EPSILON = 0.01f;

    /// <summary>
    ///     Whether the game may work out the room's list of types from what this client's game has loaded.
    /// </summary>
    public bool ObtainsTypesLocally(uGUI_MapRoomScanner scanner) => !scanner || !TryGetRoom(scanner.mapRoom, out IRoom? room) || ObtainsTypesLocally(room);

    /// <summary>
    ///     Called after the game had its chance to change the room's list of types.
    /// </summary>
    public void TypesObtained(uGUI_MapRoomScanner scanner)
    {
        if (scanner && TryGetRoom(scanner.mapRoom, out IRoom? room))
        {
            ReportTypes(room);
        }
    }

    public void ApplyTypes(MapRoomScanTypesChanged change)
    {
        if (TryGetRoom(change.MapRoomId, out IRoom? room))
        {
            ApplyTypes(room, change);
        }
    }

    internal bool ObtainsTypesLocally(IRoom room)
    {
        RoomState state = GetState(room);
        if (!state.TypesReceived || simulationOwnership.HasAnyLockType(room.Id))
        {
            return true;
        }
        // The game asks for every resource registered anywhere in the world, so the screen is only rebuilt when it
        // shows something else than the server's list.
        if (room.ShownTypes is { } shown && !shown.SetEquals(state.AvailableTypes))
        {
            room.ShowTypes(state.AvailableTypes);
        }
        return false;
    }

    /// <summary>
    ///     Sends the list of types on the room's screen to the server if this client simulates the room and the list
    ///     or the scan range changed, or the last report is a while ago.
    /// </summary>
    internal void ReportTypes(IRoom room)
    {
        if (room.ShownTypes is not { } shown || !simulationOwnership.HasAnyLockType(room.Id))
        {
            return;
        }
        RoomState state = GetState(room);
        float now = secondsSinceStartup();
        float scanRange = room.ScanRange;
        if (state.PublishedTypes != null && now - state.PublishedTime < UNCHANGED_TYPES_REFRESH_SECONDS &&
            Math.Abs(state.PublishedRange - scanRange) < RANGE_EPSILON && state.PublishedTypes.SetEquals(shown))
        {
            return;
        }
        state.PublishedTypes = [.. shown];
        state.PublishedRange = scanRange;
        state.PublishedTime = now;
        packetSender.Send(new MapRoomScanTypesReport(room.Id, shown.Select(type => type.ToDto()).ToList(), room.DetectableTypes.Select(type => type.ToDto()).ToList(),
            room.ScanOrigin.ToDto(), scanRange));
    }

    internal void ApplyTypes(IRoom room, MapRoomScanTypesChanged change) => SetAvailableTypes(room, GetState(room), change.TechTypes);

    private static void SetAvailableTypes(IRoom room, RoomState state, IEnumerable<NitroxTechType> types)
    {
        state.AvailableTypes.Clear();
        foreach (NitroxTechType type in types)
        {
            state.AvailableTypes.Add(type.ToUnity());
        }
        state.TypesReceived = true;
        room.ShowTypes(state.AvailableTypes);
    }
}
