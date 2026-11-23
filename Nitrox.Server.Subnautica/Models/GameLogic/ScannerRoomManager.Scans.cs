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
///     What a room scans for, what it found, which types it could scan for, and its fabricator. The room's simulating
///     player runs the scan and reports; the server merges in what it knows about the world itself.
/// </summary>
internal sealed partial class ScannerRoomManager
{
    internal const int MAX_SNAPSHOT_RESULTS = 10000;
    private const int MAX_RESOURCE_ID_LENGTH = 256;
    private const int MAX_SCAN_TYPES = 512;

    /// <summary>
    ///     The players who have a room's scanner screen open and are sent its results as they change.
    /// </summary>
    private readonly HashSet<(NitroxId RoomId, SessionId SessionId)> subscribers = [];

    /// <summary>
    ///     The gate for a room's scan target and scan progress. Any player who sees the room may choose a target; only
    ///     its simulating player reports progress. What is accepted is stored and goes to everyone, the sender
    ///     included, because the server sets the generation and revision. A sender that is refused has already applied
    ///     its request locally, and with the generation and revision it holds every later request would be refused as
    ///     stale too, so it is given the room's real state.
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

        if (previous == null || !Equals(previous.TypeToScan, accepted.TypeToScan) || previous.Generation != accepted.Generation)
        {
            diagnostics.RecordAccepted("scan_target", room, sessionId: sender.SessionId, reason: accepted.TypeToScan?.Name ?? "none");
        }
        return MetadataUpdateResult.AcceptAndEcho(accepted);
    }

    /// <summary>
    ///     The gate for what a room's fabricator is crafting, which only the room's simulating player reports. That
    ///     player already holds what it sent, so an accepted state only goes to the others.
    /// </summary>
    public MetadataUpdateResult AcceptFabricatorMetadata(Player sender, MapRoomEntity room, CrafterMetadata requested)
    {
        using Hold held = Enter();
        TrackRoom(room);
        if (simulationOwnershipData.GetPlayerForLock(room.Id) != sender)
        {
            logger.ZLogWarning($"Player {sender.Name} tried updating Scanner Room fabricator {room.Id} without simulation ownership");
            return MetadataUpdateResult.Reject;
        }

        CrafterMetadata? previous = room.FabricatorMetadata;
        room.FabricatorMetadata = requested;
        if (previous == null || !Equals(previous.TechType, requested.TechType) || previous.StartTime != requested.StartTime)
        {
            diagnostics.RecordAccepted("fabricator", room, sessionId: sender.SessionId, reason: requested.TechType?.Name ?? "none");
        }
        return MetadataUpdateResult.Accept(requested);
    }

    /// <summary>
    ///     The gate for a camera's light, battery and health sent as generic entity metadata: always shut. Only the
    ///     light and component packets may change them, after their ownership checks.
    /// </summary>
    public MetadataUpdateResult RejectCameraMetadata(Player sender, Entity camera)
    {
        logger.ZLogWarning($"Player {sender.Name} tried updating camera {camera.Id} state through generic metadata");
        return MetadataUpdateResult.Reject;
    }

    /// <summary>
    ///     Handles the full list of scan results a room's simulating player reports, and passes the list the server
    ///     settles on to that player and to everyone who has the room's scanner screen open.
    /// </summary>
    public async Task ReportScanAsync(Player sender, MapRoomScanResultSnapshot packet)
    {
        // Read before the gate is entered: this walks every entity of the world.
        List<WorldEntity>? worldEntities = IsSimulatedBy(packet.MapRoomId, sender) ? entityRegistry.GetEntities<WorldEntity>() : null;
        using Hold held = await EnterAsync();
        MapRoomScanResultSnapshot denial = new(packet.MapRoomId, packet.Generation, [], packet.ScanOrigin, packet.ScanRange, 0, true, false);
        if (!TryGetRoom(packet.MapRoomId, out MapRoomEntity? room))
        {
            diagnostics.RecordRejected("scan_snapshot", sessionId: sender.SessionId, reason: "unknown_room");
            await ReplyAsync(sender, denial);
            return;
        }
        if (worldEntities == null || !IsSimulatedBy(room.Id, sender))
        {
            diagnostics.RecordRejected("scan_snapshot", room, sessionId: sender.SessionId, reason: "non_owner");
            await ReplyAsync(sender, denial);
            return;
        }
        if (!TryResolveScanAnchor(entityRegistry, room, out NitroxVector3 scanAnchor))
        {
            diagnostics.RecordRejected("scan_snapshot", room, sessionId: sender.SessionId, reason: "invalid_anchor");
            await ReplyAsync(sender, denial);
            return;
        }
        if (!TryApplyScanSnapshot(room, packet, worldEntities, scanAnchor, out List<MapRoomScanResultRecord> results, out long revision))
        {
            diagnostics.RecordRejected("scan_snapshot", room, sessionId: sender.SessionId, reason: $"stale_or_invalid_gen_{packet.Generation}_room_gen_{room.ScanResultGeneration}");
            await ReplyAsync(sender, denial);
            return;
        }
        MapRoomScanResultSnapshot response = new(packet.MapRoomId, packet.Generation, results, packet.ScanOrigin, packet.ScanRange, revision, true, true);
        diagnostics.RecordAccepted("scan_snapshot", room, sessionId: sender.SessionId, reason: $"results_{results.Count}");
        await ReplyAsync(sender, response);
        await SendToSubscribersAsync(room.Id, response, sender);
    }

    /// <summary>
    ///     Handles one scan result a room's simulating player reports as found or gone, and passes it on to everyone
    ///     who has the room's scanner screen open.
    /// </summary>
    public async Task ReportScanResultAsync(Player sender, MapRoomScanResultChanged packet)
    {
        using Hold held = await EnterAsync();
        if (!TryGetRoom(packet.MapRoomId, out MapRoomEntity? room) || !IsSimulatedBy(room.Id, sender))
        {
            await ReplyAsync(sender, CreateScanResultResponse(packet, null, false));
            return;
        }
        NitroxVector3? scanAnchor = null;
        if (packet.IsRangeExit || !packet.Removed)
        {
            if (!TryResolveScanAnchor(entityRegistry, room, out NitroxVector3 resolvedAnchor))
            {
                await ReplyAsync(sender, CreateScanResultResponse(packet, null, false));
                return;
            }
            scanAnchor = resolvedAnchor;
        }
        else if (TryResolveScanAnchor(entityRegistry, room, out NitroxVector3 removalAnchor))
        {
            // Owner-only removals do not require an anchor, but exact server-backed removals use it to distinguish
            // an ordinary cell unload from a resource that is genuinely outside the current scanner query.
            scanAnchor = removalAnchor;
        }
        if (!TryApplyScanResult(room, packet, entityRegistry, scanAnchor, out bool canonicalCorrectionRequired))
        {
            if (canonicalCorrectionRequired)
            {
                await ReplyAsync(sender, CreateCorrectionSnapshot(room, packet));
            }
            else
            {
                await ReplyAsync(sender, CreateScanResultResponse(packet, null, false));
            }
            return;
        }
        MapRoomScanResultChanged response = CreateScanResultResponse(packet, room, true);
        await ReplyAsync(sender, response);
        await SendToSubscribersAsync(room.Id, response, sender);
    }

    /// <summary>
    ///     Handles the list of types a room's simulating player reports as available to scan for, and tells everyone
    ///     the list the server settles on.
    /// </summary>
    public async Task ReportScanTypesAsync(Player sender, MapRoomScanTypesSnapshot packet)
    {
        // Read before the gate is entered: this walks every entity of the world.
        List<WorldEntity>? worldEntities = IsSimulatedBy(packet.MapRoomId, sender) ? entityRegistry.GetEntities<WorldEntity>() : null;
        using Hold held = await EnterAsync();
        MapRoomScanTypesSnapshot denial = new(packet.MapRoomId, [], [], packet.ScanOrigin, packet.ScanRange, 0, true, false);
        if (!TryGetRoom(packet.MapRoomId, out MapRoomEntity? room))
        {
            diagnostics.RecordRejected("scan_types", sessionId: sender.SessionId, reason: "unknown_room");
            await ReplyAsync(sender, denial);
            return;
        }
        if (worldEntities == null || !IsSimulatedBy(room.Id, sender))
        {
            diagnostics.RecordRejected("scan_types", room, sessionId: sender.SessionId, reason: "non_owner");
            await ReplyAsync(sender, denial);
            return;
        }
        if (!TryResolveScanAnchor(entityRegistry, room, out NitroxVector3 scanAnchor))
        {
            diagnostics.RecordRejected("scan_types", room, sessionId: sender.SessionId, reason: "invalid_anchor");
            await ReplyAsync(sender, denial);
            return;
        }
        if (!TryApplyScanTypes(room, packet, worldEntities, scanAnchor, out List<NitroxTechType> accepted, out long revision))
        {
            diagnostics.RecordRejected("scan_types", room, sessionId: sender.SessionId, reason: "stale_or_invalid");
            await ReplyAsync(sender, denial);
            return;
        }
        diagnostics.RecordAccepted("scan_types", room, sessionId: sender.SessionId, reason: $"types_{accepted.Count}", stateRevision: revision);
        await SendToAllAsync(new MapRoomScanTypesSnapshot(packet.MapRoomId, accepted, [], packet.ScanOrigin, packet.ScanRange, revision, true, true));
    }

    /// <summary>
    ///     Starts or stops sending a room's scan results to a player. A player who starts is sent the current results.
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
        await ReplyAsync(player, new MapRoomScanResultSnapshot(room.Id, room.ScanResultGeneration, CloneResults(room.ScanResults), NitroxVector3.Zero, 0f,
            room.ScanResultRevision, true, true));
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
        room.BeginScanResultGeneration(accepted.Generation);
        return true;
    }

    /// <summary>
    ///     A request is only accepted if it was made against the state the room is in. A new target starts a new
    ///     generation, which empties the results; more nodes scanned of the same target is progress.
    /// </summary>
    internal static bool TryAcceptScanMetadata(MapRoomMetadata? current, MapRoomMetadata requested, out MapRoomMetadata accepted)
    {
        current ??= new MapRoomMetadata(NitroxTechType.None, 0);
        accepted = current;

        if (requested.Generation != current.Generation || requested.Revision != current.Revision)
        {
            return false;
        }

        if (!requested.TypeToScan.Equals(current.TypeToScan))
        {
            accepted = new MapRoomMetadata(requested.TypeToScan, 0, current.Generation + 1, current.Revision + 1);
            return true;
        }

        if (requested.NumNodesScanned <= current.NumNodesScanned)
        {
            return false;
        }

        accepted = new MapRoomMetadata(current.TypeToScan, requested.NumNodesScanned, current.Generation, current.Revision + 1);
        return true;
    }

    internal static bool TryApplyScanSnapshot(MapRoomEntity room, MapRoomScanResultSnapshot requested, IEnumerable<WorldEntity> worldEntities, NitroxVector3 scanAnchor,
        out List<MapRoomScanResultRecord> acceptedResults, out long revision)
    {
        acceptedResults = [];
        revision = room.ScanResultRevision;
        if (requested.IsServerResponse || requested.MapRoomId != room.Id || requested.Generation != room.ScanResultGeneration || requested.Results == null || requested.Results.Count > MAX_SNAPSHOT_RESULTS ||
            worldEntities == null || room.Metadata is not MapRoomMetadata metadata ||
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

        List<MapRoomScanResultRecord> merged = MergeScanResults(requested.Results, worldEntities, metadata.TypeToScan, scanOrigin, scanRange, MAX_SNAPSHOT_RESULTS);
        if (ResultsEqual(room.ScanResults, merged))
        {
            acceptedResults = CloneResults(room.ScanResults);
            return true;
        }
        room.ScanResults = CloneResults(merged);
        room.ScanResultRevision++;
        revision = room.ScanResultRevision;
        acceptedResults = CloneResults(room.ScanResults);
        return true;
    }

    internal static bool TryApplyScanResult(MapRoomEntity room, MapRoomScanResultChanged requested, EntityRegistry entityRegistry, NitroxVector3? scanAnchor,
        out bool canonicalCorrectionRequired)
    {
        canonicalCorrectionRequired = false;
        if (requested.IsServerResponse || requested.MapRoomId != room.Id || !IsValidResourceId(requested.ResourceId) || requested.TechType == null ||
            requested.Generation != room.ScanResultGeneration || (!requested.Removed && !IsFinite(requested.Position)) ||
            requested.IsRangeExit && !requested.Removed || entityRegistry == null || room.Metadata is not MapRoomMetadata metadata ||
            metadata.TypeToScan.Equals(NitroxTechType.None) || !requested.TechType.Equals(metadata.TypeToScan))
        {
            return false;
        }
        if (requested.Removed)
        {
            MapRoomScanResultRecord? current = room.ScanResults.Find(result => result.ResourceId == requested.ResourceId);
            if (current == null || !current.TechType.Equals(metadata.TypeToScan))
            {
                return false;
            }

            bool hasLiveExactEntity = TryGetLiveExactWorldEntity(entityRegistry, requested.ResourceId, metadata.TypeToScan, out WorldEntity liveExactEntity);
            if (!requested.IsRangeExit)
            {
                // A local ResourceTracker can unregister when its cell unloads. The server registry remains canonical
                // for exact-TechType supplements, while owner-only override mappings retain vanilla removal behavior.
                if (hasLiveExactEntity)
                {
                    if (!scanAnchor.HasValue ||
                        !TryNormalizeScanQuery(requested.ScanOrigin, requested.ScanRange, scanAnchor.Value, out NitroxVector3 removalScanOrigin, out float removalScanRange) ||
                        !IsScanRangeAllowed(room, removalScanRange) ||
                        !IsFinite(liveExactEntity.Transform.Position))
                    {
                        // Do not amplify an invalid request into a full corrective snapshot.
                        return false;
                    }
                    if (IsWithinRange(removalScanOrigin, liveExactEntity.Transform.Position, removalScanRange))
                    {
                        canonicalCorrectionRequired = true;
                        return false;
                    }
                    return room.TryRemoveScanResult(requested.Generation, requested.ResourceId);
                }
                return room.TryRemoveScanResult(requested.Generation, requested.ResourceId);
            }

            if (!scanAnchor.HasValue ||
                !TryNormalizeScanQuery(requested.ScanOrigin, requested.ScanRange, scanAnchor.Value, out NitroxVector3 scanOrigin, out float scanRange) ||
                !IsScanRangeAllowed(room, scanRange))
            {
                return false;
            }

            NitroxVector3 position = hasLiveExactEntity ? liveExactEntity.Transform.Position : requested.Position;
            if (!IsFinite(position))
            {
                return false;
            }
            if (IsWithinRange(scanOrigin, position, scanRange))
            {
                // Vanilla has already removed an outside-reported node locally. Restore exact server-backed nodes
                // when the authoritative transform still belongs to this scanner query.
                canonicalCorrectionRequired = hasLiveExactEntity;
                return false;
            }
            return room.TryRemoveScanResult(requested.Generation, requested.ResourceId);
        }
        if (!scanAnchor.HasValue ||
            !TryNormalizeScanQuery(requested.ScanOrigin, requested.ScanRange, scanAnchor.Value, out NitroxVector3 addedScanOrigin, out float addedScanRange) ||
            !IsScanRangeAllowed(room, addedScanRange))
        {
            return false;
        }
        NitroxVector3 acceptedPosition = TryGetLiveExactWorldEntity(entityRegistry, requested.ResourceId, metadata.TypeToScan,
            out WorldEntity authoritativeEntity) ? authoritativeEntity.Transform.Position : requested.Position;
        if (!IsFinite(acceptedPosition) || !IsWithinRange(addedScanOrigin, acceptedPosition, addedScanRange))
        {
            return false;
        }
        bool isNewResult = room.ScanResults.TrueForAll(result => result.ResourceId != requested.ResourceId);
        if (isNewResult && room.ScanResults.Count >= MAX_SNAPSHOT_RESULTS)
        {
            return false;
        }
        return room.TryApplyScanResult(requested.Generation, new MapRoomScanResultRecord(requested.ResourceId, requested.TechType, acceptedPosition));
    }

    /// <summary>
    ///     Takes an entity that left the world out of the scan results of every room that had found it.
    /// </summary>
    /// <returns>What the players who watch those rooms have to be told.</returns>
    internal static List<MapRoomScanResultChanged> InvalidateScanResult(IEnumerable<MapRoomEntity> rooms, NitroxId resourceId)
    {
        string id = resourceId.ToString();
        List<MapRoomScanResultChanged> removals = [];
        foreach (MapRoomEntity room in rooms)
        {
            MapRoomScanResultRecord? result = room.ScanResults.Find(record => record.ResourceId == id);
            if (result == null || !room.TryRemoveScanResult(room.ScanResultGeneration, id))
            {
                continue;
            }
            removals.Add(new MapRoomScanResultChanged(room.Id, room.ScanResultGeneration, id, result.TechType, result.Position,
                removed: true, isRangeExit: false, scanOrigin: NitroxVector3.Zero, scanRange: 0f,
                revision: room.ScanResultRevision, isServerResponse: true, granted: true));
        }
        return removals;
    }

    internal static bool TryApplyScanTypes(MapRoomEntity room, MapRoomScanTypesSnapshot requested, IEnumerable<WorldEntity> worldEntities, NitroxVector3 scanAnchor,
        out List<NitroxTechType> accepted, out long revision)
    {
        accepted = [];
        revision = room.AvailableScanTypesRevision;
        if (requested.IsServerResponse || requested.MapRoomId != room.Id || requested.TechTypes == null || requested.TechTypes.Count > MAX_SCAN_TYPES ||
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
        List<NitroxTechType> normalized = MergeScanTypes(unique, detectable, worldEntities, scanOrigin, scanRange);
        if (room.AvailableScanTypesRevision > 0 && room.AvailableScanTypes.SequenceEqual(normalized))
        {
            accepted = room.AvailableScanTypes.ToList();
            return true;
        }
        room.AvailableScanTypes = normalized;
        room.AvailableScanTypesRevision++;
        revision = room.AvailableScanTypesRevision;
        accepted = room.AvailableScanTypes.ToList();
        return true;
    }

    /// <summary>
    ///     The full result list for the room's simulating player, whose own list has drifted from the server's.
    /// </summary>
    internal static MapRoomScanResultSnapshot CreateCorrectionSnapshot(MapRoomEntity room, MapRoomScanResultChanged request) =>
        new(room.Id, room.ScanResultGeneration, CloneResults(room.ScanResults), request.ScanOrigin, request.ScanRange, room.ScanResultRevision, true, true);

    private static MapRoomScanResultChanged CreateScanResultResponse(MapRoomScanResultChanged packet, MapRoomEntity? room, bool granted)
    {
        NitroxVector3 position = packet.Position;
        long revision = 0;
        if (room != null)
        {
            revision = room.ScanResultRevision;
            if (granted && !packet.Removed)
            {
                MapRoomScanResultRecord? canonical = room.ScanResults.Find(result => result.ResourceId == packet.ResourceId);
                position = canonical?.Position ?? position;
            }
        }
        return new MapRoomScanResultChanged(packet.MapRoomId, packet.Generation, packet.ResourceId, packet.TechType, position,
            packet.Removed, packet.IsRangeExit, packet.ScanOrigin, packet.ScanRange, revision, true, granted);
    }

    private bool IsSimulatedBy(NitroxId? roomId, Player player) => roomId != null && simulationOwnershipData.GetPlayerForLock(roomId) == player;

    /// <summary>
    ///     Sends a room's scan change to the connected players who have its scanner screen open.
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

    private static bool ResultsEqual(IReadOnlyList<MapRoomScanResultRecord> current, IReadOnlyList<MapRoomScanResultRecord> requested) =>
        current.Count == requested.Count && current.Zip(requested, (left, right) =>
            left.ResourceId == right.ResourceId && left.TechType.Equals(right.TechType) && left.Position.Equals(right.Position)).All(equal => equal);

    private static List<MapRoomScanResultRecord> CloneResults(IEnumerable<MapRoomScanResultRecord> results) =>
        results.Select(result => new MapRoomScanResultRecord(result.ResourceId, result.TechType, result.Position)).ToList();

    private static bool TryGetLiveExactWorldEntity(EntityRegistry entityRegistry, string resourceId, NitroxTechType target, out WorldEntity worldEntity)
    {
        worldEntity = null!;
        return Guid.TryParse(resourceId, out Guid guid) &&
               entityRegistry.TryGetEntityById(new NitroxId(guid), out worldEntity!) &&
               worldEntity.TechType != null && worldEntity.TechType.Equals(target) && worldEntity.Transform != null;
    }
}
