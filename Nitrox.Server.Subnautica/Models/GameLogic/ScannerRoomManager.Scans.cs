using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     What a room scans for, what it found and which types it could scan for. The target and the
///     progress are the room's metadata. What the scan found and the types are held here, in memory only, as in the
///     game, which saves the target and the progress and scans again when it starts. The room's simulating player runs
///     the scan and reports; the server adds what it knows of the world itself and tells the subscribers.
/// </summary>
internal sealed partial class ScannerRoomManager
{
    internal const int MAX_SNAPSHOT_RESULTS = 10000;
    private const int MAX_RESOURCE_ID_LENGTH = 256;
    private const int MAX_SCAN_TYPES = 512;

    /// <summary>
    ///     What every room's scan has found. Empty after a server start, until the room's simulating player reports.
    /// </summary>
    private readonly Dictionary<NitroxId, RoomScan> scansByRoomId = [];

    /// <summary>
    ///     The players who are sent a room's results and types as they change.
    /// </summary>
    private readonly HashSet<(NitroxId RoomId, SessionId SessionId)> subscribers = [];

    /// <summary>
    ///     The gate for a room's scan target and scan progress. Any player who sees the room may choose a target; only
    ///     its simulating player reports progress. A new target goes to everyone, the sender included, because the
    ///     server sets its generation; progress only goes to the others. A sender that is refused has already applied
    ///     its request locally, and with the generation it holds every later request would be refused as outdated too,
    ///     so it is given the room's real state.
    /// </summary>
    public MetadataUpdateResult AcceptRoomMetadata(Player sender, Entity target, MapRoomMetadata requested)
    {
        if (target is not MapRoomEntity room)
        {
            logger.ZLogWarning($"Player {sender.Name} tried applying Map Room metadata to non-Map Room entity {target.Id}");
            return MetadataUpdateResult.Reject;
        }

        using Hold held = Enter();
        TrackRoom(room);
        MapRoomMetadata? previous = room.Metadata as MapRoomMetadata;
        MapRoomMetadata stored = previous ?? new MapRoomMetadata(NitroxTechType.None, 0);
        if (!sender.CanSee(room))
        {
            logger.ZLogWarning($"Player {sender.Name} tried updating Map Room {room.Id} while it was not visible");
            return MetadataUpdateResult.RejectAndCorrect(stored);
        }
        if (IsScanProgress(previous, requested) && simulationOwnershipData.GetPlayerForLock(room.Id) != sender)
        {
            diagnostics.RecordRejected("scan_progress", room, sessionId: sender.SessionId, reason: "non_owner");
            return MetadataUpdateResult.RejectAndCorrect(stored);
        }
        if (!TryApplyScanMetadata(room, requested, out MapRoomMetadata accepted))
        {
            logger.ZLogDebug($"Rejected stale or duplicate Map Room metadata for {room.Id}: requested {requested}, current {room.Metadata}");
            diagnostics.RecordStaleScanRequest(room, sender.SessionId, requested);
            return MetadataUpdateResult.RejectAndCorrect(stored);
        }

        if (previous != null && Equals(previous.TypeToScan, accepted.TypeToScan) && previous.Generation == accepted.Generation)
        {
            return MetadataUpdateResult.Accept(accepted);
        }
        if (scansByRoomId.TryGetValue(room.Id, out RoomScan? scan))
        {
            scan.Begin(accepted.Generation);
        }
        diagnostics.RecordAccepted("scan_target", room, sessionId: sender.SessionId, reason: accepted.TypeToScan?.Name ?? "none");
        return MetadataUpdateResult.AcceptAndEcho(accepted);
    }

    /// <summary>
    ///     Handles the full list of scan results a room's simulating player reports. The list the server settles on
    ///     goes back to that player, whose own list lacks what its game has not loaded, and to the subscribers if it
    ///     differs from what they were told before.
    /// </summary>
    public async Task ReportScanAsync(Player sender, MapRoomScanReport packet)
    {
        // Read before the gate is entered: this walks every entity of the world.
        List<WorldEntity>? worldEntities = IsSimulatedBy(packet.MapRoomId, sender) ? entityRegistry.GetEntities<WorldEntity>() : null;
        using Hold held = await EnterAsync();
        if (!TryGetRoom(packet.MapRoomId, out MapRoomEntity? room))
        {
            diagnostics.RecordRejected("scan_snapshot", sessionId: sender.SessionId, reason: "unknown_room");
            return;
        }
        if (worldEntities == null || !IsSimulatedBy(room.Id, sender))
        {
            diagnostics.RecordRejected("scan_snapshot", room, sessionId: sender.SessionId, reason: "non_owner");
            return;
        }
        if (!TryResolveScanAnchor(entityRegistry, room, out NitroxVector3 scanAnchor))
        {
            diagnostics.RecordRejected("scan_snapshot", room, sessionId: sender.SessionId, reason: "invalid_anchor");
            return;
        }
        RoomScan scan = GetScan(room);
        if (!TryApplyScanReport(room, scan, packet, worldEntities, scanAnchor, out bool changed))
        {
            diagnostics.RecordRejected("scan_snapshot", room, sessionId: sender.SessionId, reason: $"stale_or_invalid_gen_{packet.Generation}_room_gen_{scan.Generation}");
            return;
        }
        diagnostics.RecordAccepted("scan_snapshot", room, sessionId: sender.SessionId, reason: $"results_{scan.Results.Count}");
        MapRoomScanSnapshot snapshot = CreateSnapshot(room, scan);
        await ReplyAsync(sender, snapshot);
        if (changed)
        {
            await SendToSubscribersAsync(room.Id, snapshot, sender);
        }
    }

    /// <summary>
    ///     Handles one scan result a room's simulating player reports as found or gone, and passes it on to the
    ///     subscribers. A report the server knows better than (the resource still exists and is in range) is answered
    ///     with the full list, because the sender's own list has drifted.
    /// </summary>
    public async Task ReportScanResultAsync(Player sender, MapRoomScanResultReport packet)
    {
        using Hold held = await EnterAsync();
        if (!TryGetRoom(packet.MapRoomId, out MapRoomEntity? room) || !IsSimulatedBy(room.Id, sender))
        {
            diagnostics.RecordRejected("scan_snapshot", room, sessionId: sender.SessionId, reason: room == null ? "result_unknown_room" : "result_non_owner");
            return;
        }
        NitroxVector3? scanAnchor = TryResolveScanAnchor(entityRegistry, room, out NitroxVector3 resolvedAnchor) ? resolvedAnchor : null;
        RoomScan scan = GetScan(room);
        switch (ApplyScanResultReport(room, scan, packet, entityRegistry, scanAnchor, out MapRoomScanResultRecord? accepted))
        {
            case ScanResultOutcome.CHANGED:
                MapRoomScanResultChanged change = new(room.Id, scan.Generation, accepted!, packet.Removed);
                await ReplyAsync(sender, change);
                await SendToSubscribersAsync(room.Id, change, sender);
                break;
            case ScanResultOutcome.CORRECTION_REQUIRED:
                await ReplyAsync(sender, CreateSnapshot(room, scan));
                break;
            case ScanResultOutcome.REFUSED:
                diagnostics.RecordRejected("scan_snapshot", room, sessionId: sender.SessionId, reason: $"result_stale_or_invalid_gen_{packet.Generation}_room_gen_{scan.Generation}");
                break;
        }
    }

    /// <summary>
    ///     Handles the list of types a room's simulating player reports as available to scan for. The list the server
    ///     settles on goes back to that player and, if it changed, to the subscribers.
    /// </summary>
    public async Task ReportScanTypesAsync(Player sender, MapRoomScanTypesReport packet)
    {
        // Read before the gate is entered: this walks every entity of the world.
        List<WorldEntity>? worldEntities = IsSimulatedBy(packet.MapRoomId, sender) ? entityRegistry.GetEntities<WorldEntity>() : null;
        using Hold held = await EnterAsync();
        if (!TryGetRoom(packet.MapRoomId, out MapRoomEntity? room))
        {
            diagnostics.RecordRejected("scan_types", sessionId: sender.SessionId, reason: "unknown_room");
            return;
        }
        if (worldEntities == null || !IsSimulatedBy(room.Id, sender))
        {
            diagnostics.RecordRejected("scan_types", room, sessionId: sender.SessionId, reason: "non_owner");
            return;
        }
        if (!TryResolveScanAnchor(entityRegistry, room, out NitroxVector3 scanAnchor))
        {
            diagnostics.RecordRejected("scan_types", room, sessionId: sender.SessionId, reason: "invalid_anchor");
            return;
        }
        RoomScan scan = GetScan(room);
        if (!TryApplyScanTypesReport(room, scan, packet, worldEntities, scanAnchor, out bool changed))
        {
            diagnostics.RecordRejected("scan_types", room, sessionId: sender.SessionId, reason: "stale_or_invalid");
            return;
        }
        diagnostics.RecordAccepted("scan_types", room, sessionId: sender.SessionId, reason: $"types_{scan.AvailableTypes.Count}", stateRevision: scan.AvailableTypesVersion);
        MapRoomScanTypesChanged types = new(room.Id, [.. scan.AvailableTypes]);
        await ReplyAsync(sender, types);
        if (changed)
        {
            await SendToSubscribersAsync(room.Id, types, sender);
        }
    }

    /// <summary>
    ///     Starts or stops sending a room's scan results and types to a player. A player who starts is sent what the
    ///     server holds, which is nothing after a server start until the room's simulating player has reported.
    /// </summary>
    public async Task SetSubscriptionAsync(Player player, NitroxId roomId, bool subscribed)
    {
        using Hold held = await EnterAsync();
        if (!TryGetRoom(roomId, out MapRoomEntity? room))
        {
            return;
        }
        if (!subscribed)
        {
            subscribers.Remove((roomId, player.SessionId));
            return;
        }
        subscribers.Add((roomId, player.SessionId));
        await ReplyAsync(player, CreateSnapshot(room, GetScan(room)));
    }

    internal static bool IsScanProgress(MapRoomMetadata? current, MapRoomMetadata requested) =>
        current != null && requested.TypeToScan.Equals(current.TypeToScan) && requested.NumNodesScanned > current.NumNodesScanned;

    /// <summary>
    ///     Stores a scan target or scan progress request in the room if <see cref="TryAcceptScanMetadata" /> accepts it.
    /// </summary>
    internal static bool TryApplyScanMetadata(MapRoomEntity room, MapRoomMetadata requested, out MapRoomMetadata accepted)
    {
        if (!TryAcceptScanMetadata(room.Metadata as MapRoomMetadata, requested, out accepted))
        {
            return false;
        }
        room.Metadata = accepted;
        return true;
    }

    /// <summary>
    ///     A request is only accepted if it was made against the target the room has now, which its generation tells.
    ///     A new target starts a new generation, with nothing scanned yet; more nodes scanned of the same target is
    ///     progress.
    /// </summary>
    internal static bool TryAcceptScanMetadata(MapRoomMetadata? current, MapRoomMetadata requested, out MapRoomMetadata accepted)
    {
        current ??= new MapRoomMetadata(NitroxTechType.None, 0);
        accepted = current;

        if (requested.Generation != current.Generation)
        {
            return false;
        }

        if (!requested.TypeToScan.Equals(current.TypeToScan))
        {
            accepted = new MapRoomMetadata(requested.TypeToScan, 0, current.Generation + 1);
            return true;
        }

        if (requested.NumNodesScanned <= current.NumNodesScanned)
        {
            return false;
        }

        accepted = new MapRoomMetadata(current.TypeToScan, requested.NumNodesScanned, current.Generation);
        return true;
    }

    /// <summary>
    ///     Checks a full list of results against the room's target and scan range, adds the matching entities the
    ///     server knows in range, and stores the outcome.
    /// </summary>
    /// <param name="changed">Whether the stored list differs from the one before.</param>
    internal static bool TryApplyScanReport(MapRoomEntity room, RoomScan scan, MapRoomScanReport requested, IEnumerable<WorldEntity> worldEntities, NitroxVector3 scanAnchor,
        out bool changed)
    {
        changed = false;
        if (requested.MapRoomId != room.Id || room.Metadata is not MapRoomMetadata metadata || requested.Generation != metadata.Generation ||
            requested.Results == null || requested.Results.Count > MAX_SNAPSHOT_RESULTS || worldEntities == null ||
            !TryNormalizeScanQuery(requested.ScanOrigin, requested.ScanRange, scanAnchor, out NitroxVector3 scanOrigin, out float scanRange))
        {
            return false;
        }
        if (!IsScanRangeAllowed(room, scanRange))
        {
            return false;
        }
        if (metadata.TypeToScan.Equals(NitroxTechType.None) && requested.Results.Count != 0)
        {
            return false;
        }
        HashSet<string> ids = [];
        foreach (MapRoomScanResultRecord result in requested.Results)
        {
            if (result == null || !IsValidResourceId(result.ResourceId) || result.TechType == null || !IsFinite(result.Position) || !ids.Add(result.ResourceId) ||
                !result.TechType.Equals(metadata.TypeToScan) || !IsWithinRange(scanOrigin, result.Position, scanRange))
            {
                return false;
            }
        }

        scan.Begin(metadata.Generation);
        changed = scan.TryReplace(MergeScanResults(requested.Results, worldEntities, metadata.TypeToScan, scanOrigin, scanRange, MAX_SNAPSHOT_RESULTS));
        return true;
    }

    /// <summary>
    ///     Checks one found or lost result against the room's target and scan range, and against the entity the server
    ///     has for it, and stores it.
    /// </summary>
    /// <param name="accepted">The result as stored or removed. Its position is the server's where the server has the entity.</param>
    internal static ScanResultOutcome ApplyScanResultReport(MapRoomEntity room, RoomScan scan, MapRoomScanResultReport requested, EntityRegistry entityRegistry,
        NitroxVector3? scanAnchor, out MapRoomScanResultRecord? accepted)
    {
        accepted = null;
        MapRoomScanResultRecord? reported = requested.Result;
        if (requested.MapRoomId != room.Id || reported == null || !IsValidResourceId(reported.ResourceId) || reported.TechType == null ||
            room.Metadata is not MapRoomMetadata metadata || requested.Generation != metadata.Generation || (!requested.Removed && !IsFinite(reported.Position)) ||
            requested.IsRangeExit && !requested.Removed || entityRegistry == null ||
            metadata.TypeToScan.Equals(NitroxTechType.None) || !reported.TechType.Equals(metadata.TypeToScan))
        {
            return ScanResultOutcome.REFUSED;
        }
        scan.Begin(metadata.Generation);
        bool hasLiveExactEntity = TryGetLiveExactWorldEntity(entityRegistry, reported.ResourceId, metadata.TypeToScan, out WorldEntity liveExactEntity);
        bool hasQuery = TryGetScanQuery(room, requested.ScanOrigin, requested.ScanRange, scanAnchor, out NitroxVector3 scanOrigin, out float scanRange);
        if (requested.Removed)
        {
            MapRoomScanResultRecord? current = scan.Results.Find(result => result.ResourceId == reported.ResourceId);
            if (current == null)
            {
                return ScanResultOutcome.UNCHANGED;
            }
            if (!requested.IsRangeExit && !hasLiveExactEntity)
            {
                // Only the sender ever knew this resource, so the server takes its word that it is gone.
                return Remove(scan, current, out accepted);
            }
            if (!hasQuery)
            {
                // Without a valid scan query the server cannot tell an unloaded resource from one out of range, and
                // an invalid request is not answered with a full list.
                return ScanResultOutcome.REFUSED;
            }

            NitroxVector3 position = hasLiveExactEntity ? liveExactEntity.Transform.Position : reported.Position;
            if (!IsFinite(position))
            {
                return ScanResultOutcome.REFUSED;
            }
            if (!IsWithinRange(scanOrigin, position, scanRange))
            {
                return Remove(scan, current, out accepted);
            }
            // A resource the server has, in range, is still there: the sender's game unloaded its cell, or reported
            // it outside from a position the server does not share. The sender has already dropped it and gets the
            // list back.
            return hasLiveExactEntity ? ScanResultOutcome.CORRECTION_REQUIRED : ScanResultOutcome.REFUSED;
        }

        if (!hasQuery)
        {
            return ScanResultOutcome.REFUSED;
        }
        NitroxVector3 acceptedPosition = hasLiveExactEntity ? liveExactEntity.Transform.Position : reported.Position;
        if (!IsFinite(acceptedPosition) || !IsWithinRange(scanOrigin, acceptedPosition, scanRange))
        {
            return ScanResultOutcome.REFUSED;
        }
        bool isNewResult = scan.Results.TrueForAll(result => result.ResourceId != reported.ResourceId);
        if (isNewResult && scan.Results.Count >= MAX_SNAPSHOT_RESULTS)
        {
            return ScanResultOutcome.REFUSED;
        }
        accepted = new MapRoomScanResultRecord(reported.ResourceId, reported.TechType, acceptedPosition);
        return scan.TryApply(accepted) ? ScanResultOutcome.CHANGED : ScanResultOutcome.UNCHANGED;
    }

    /// <summary>
    ///     Checks a list of available scan types, adds the types of the entities the server knows in range, and stores
    ///     the outcome.
    /// </summary>
    /// <param name="changed">Whether the stored list differs from the one before, or is the first one.</param>
    internal static bool TryApplyScanTypesReport(MapRoomEntity room, RoomScan scan, MapRoomScanTypesReport requested, IEnumerable<WorldEntity> worldEntities,
        NitroxVector3 scanAnchor, out bool changed)
    {
        changed = false;
        if (requested.MapRoomId != room.Id || requested.TechTypes == null || requested.TechTypes.Count > MAX_SCAN_TYPES ||
            requested.DetectableTechTypes == null || requested.DetectableTechTypes.Count > MAX_SCAN_TYPES || worldEntities == null ||
            !TryNormalizeScanQuery(requested.ScanOrigin, requested.ScanRange, scanAnchor, out NitroxVector3 scanOrigin, out float scanRange))
        {
            return false;
        }
        if (!IsScanRangeAllowed(room, scanRange))
        {
            return false;
        }
        HashSet<NitroxTechType> unique = [];
        foreach (NitroxTechType techType in requested.TechTypes)
        {
            if (techType == null || techType.Equals(NitroxTechType.None) || !unique.Add(techType))
            {
                return false;
            }
        }
        HashSet<NitroxTechType> detectable = [];
        foreach (NitroxTechType techType in requested.DetectableTechTypes)
        {
            if (techType == null || techType.Equals(NitroxTechType.None) || !detectable.Add(techType))
            {
                return false;
            }
        }
        changed = scan.TrySetAvailableTypes(MergeScanTypes(unique, detectable, worldEntities, scanOrigin, scanRange));
        return true;
    }

    private static ScanResultOutcome Remove(RoomScan scan, MapRoomScanResultRecord current, out MapRoomScanResultRecord? removed)
    {
        removed = current;
        return scan.TryRemove(current.ResourceId) ? ScanResultOutcome.CHANGED : ScanResultOutcome.UNCHANGED;
    }

    private static bool TryGetScanQuery(MapRoomEntity room, NitroxVector3 requestedOrigin, float requestedRange, NitroxVector3? scanAnchor, out NitroxVector3 scanOrigin,
        out float scanRange)
    {
        scanOrigin = NitroxVector3.Zero;
        scanRange = 0f;
        return scanAnchor.HasValue && TryNormalizeScanQuery(requestedOrigin, requestedRange, scanAnchor.Value, out scanOrigin, out scanRange) &&
               IsScanRangeAllowed(room, scanRange);
    }

    /// <summary>
    ///     What the server holds for a room's scan, for the generation of the target the room has now.
    /// </summary>
    private RoomScan GetScan(MapRoomEntity room)
    {
        if (!scansByRoomId.TryGetValue(room.Id, out RoomScan? scan))
        {
            scan = new RoomScan();
            scansByRoomId[room.Id] = scan;
        }
        scan.Begin((room.Metadata as MapRoomMetadata)?.Generation ?? 0);
        return scan;
    }

    private static MapRoomScanSnapshot CreateSnapshot(MapRoomEntity room, RoomScan scan) =>
        new(room.Id, scan.Generation, CloneResults(scan.Results), [.. scan.AvailableTypes]);

    /// <summary>
    ///     Takes an entity that left the world out of the scan results of every room that had found it, and tells the
    ///     players who are sent those rooms' results.
    /// </summary>
    private async Task RemoveFromScanResultsAsync(NitroxId entityId)
    {
        string resourceId = entityId.ToString();
        foreach (MapRoomEntity room in GetRooms())
        {
            if (!scansByRoomId.ContainsKey(room.Id))
            {
                continue;
            }
            RoomScan scan = GetScan(room);
            if (scan.Results.Find(result => result.ResourceId == resourceId) is { } found && scan.TryRemove(resourceId))
            {
                await SendToSubscribersAsync(room.Id, new MapRoomScanResultChanged(room.Id, scan.Generation, found, true));
            }
        }
    }

    private bool IsSimulatedBy(NitroxId? roomId, Player player) => roomId != null && simulationOwnershipData.GetPlayerForLock(roomId) == player;

    /// <summary>
    ///     Sends a room's scan change to the connected players who subscribed to it.
    /// </summary>
    private async Task SendToSubscribersAsync<T>(NitroxId roomId, T packet, Player? except = null) where T : Packet
    {
        if (!subscribers.Any(subscriber => subscriber.RoomId == roomId))
        {
            return;
        }
        foreach (Player player in playerManager.GetConnectedPlayers())
        {
            if (player != except && subscribers.Contains((roomId, player.SessionId)))
            {
                await packetSender.SendPacketAsync(packet, player.SessionId);
            }
        }
    }

    private static bool IsValidResourceId(string resourceId) => !string.IsNullOrEmpty(resourceId) && resourceId.Length <= MAX_RESOURCE_ID_LENGTH;

    private static List<MapRoomScanResultRecord> CloneResults(IEnumerable<MapRoomScanResultRecord> results) =>
        results.Select(result => new MapRoomScanResultRecord(result.ResourceId, result.TechType, result.Position)).ToList();

    private static bool TryGetLiveExactWorldEntity(EntityRegistry entityRegistry, string resourceId, NitroxTechType target, out WorldEntity worldEntity)
    {
        worldEntity = null!;
        return Guid.TryParse(resourceId, out Guid guid) &&
               entityRegistry.TryGetEntityById(new NitroxId(guid), out worldEntity!) &&
               worldEntity.TechType != null && worldEntity.TechType.Equals(target) && worldEntity.Transform != null;
    }

    /// <summary>
    ///     What became of one reported scan result.
    /// </summary>
    internal enum ScanResultOutcome
    {
        /// <summary>Stored; the subscribers are told.</summary>
        CHANGED,

        /// <summary>Nothing new: the server already held this, or never held what was reported gone.</summary>
        UNCHANGED,

        /// <summary>Not allowed or not valid, and dropped.</summary>
        REFUSED,

        /// <summary>Dropped, and the sender's own list is now wrong and has to be replaced.</summary>
        CORRECTION_REQUIRED
    }

    /// <summary>
    ///     What one room's scan has found for the target it has now, and the types it offers to scan for.
    /// </summary>
    internal sealed class RoomScan
    {
        /// <summary>
        ///     The generation of the scan target the results belong to.
        /// </summary>
        public long Generation { get; private set; }

        public List<MapRoomScanResultRecord> Results { get; private set; } = [];

        public List<NitroxTechType> AvailableTypes { get; private set; } = [];

        /// <summary>
        ///     Counts how often the list of types changed. 0 as long as nobody has reported one.
        /// </summary>
        public long AvailableTypesVersion { get; private set; }

        /// <summary>
        ///     Starts over for another scan target. The results of the previous one are of no use to it.
        /// </summary>
        public void Begin(long generation)
        {
            if (generation != Generation)
            {
                Generation = generation;
                Results = [];
            }
        }

        public bool TryReplace(List<MapRoomScanResultRecord> results)
        {
            if (Results.Count == results.Count && Results.Zip(results, (left, right) =>
                    left.ResourceId == right.ResourceId && left.TechType.Equals(right.TechType) && left.Position.Equals(right.Position)).All(equal => equal))
            {
                return false;
            }
            Results = CloneResults(results);
            return true;
        }

        public bool TryApply(MapRoomScanResultRecord result)
        {
            MapRoomScanResultRecord? current = Results.Find(record => record.ResourceId == result.ResourceId);
            if (current == null)
            {
                Results.Add(result);
                return true;
            }
            if (current.TechType.Equals(result.TechType) && current.Position.Equals(result.Position))
            {
                return false;
            }
            current.TechType = result.TechType;
            current.Position = result.Position;
            return true;
        }

        public bool TryRemove(string resourceId) => Results.RemoveAll(record => record.ResourceId == resourceId) > 0;

        public bool TrySetAvailableTypes(List<NitroxTechType> types)
        {
            if (AvailableTypesVersion > 0 && AvailableTypes.SequenceEqual(types))
            {
                return false;
            }
            AvailableTypes = types;
            AvailableTypesVersion++;
            return true;
        }
    }
}
