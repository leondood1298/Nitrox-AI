using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Helper;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Server.Subnautica.Models.Commands;

/// <summary>
///     Prints what the server holds for every Scanner Room and camera. It only reads: running it must not change the
///     counters or the history it reports, or two runs could not be compared.
/// </summary>
[RequiresPermission(Perms.ADMIN)]
internal sealed class ScannerRoomCommand(EntityRegistry entityRegistry, ScannerRoomDiagnostics diagnostics, SimulationOwnershipData simulationOwnershipData,
    ScannerRoomManager scannerRoomManager) : ICommandHandler
{
    private const int RECENT_ENTRY_LIMIT = 25;
    private static readonly NitroxTechType mapRoomCameraTechType = new("MapRoomCamera");

    [Description("Lists Scanner Room totals and anomalies, then every room and camera with its locks, then the recent transitions")]
    public async Task Execute(ICommandContext context)
    {
        // Copies of all rooms from one moment, ordered by id. Nothing below can change what the server holds.
        MapRoomEntity[] rooms = scannerRoomManager.GetSnapshots().Select(snapshot => snapshot.Room).ToArray();
        List<string> anomalies = [];
        List<RoomView> roomViews = [];
        Dictionary<NitroxId, int> registrations = [];
        Dictionary<NitroxId, int> dockings = [];
        foreach (MapRoomEntity room in rooms)
        {
            RoomView view = ReadRoom(room);
            roomViews.Add(view);
            if (view.Violation != null)
            {
                anomalies.Add($"room {ShortRoom(room.Id)} breaks an invariant: {view.Violation}");
            }
            if (!simulationOwnershipData.TryGetLock(room.Id, out _))
            {
                anomalies.Add($"room {ShortRoom(room.Id)} has no simulating player: scan results, scan types and docked camera recharge are only accepted from that player");
            }
            foreach (CameraView camera in view.Cameras)
            {
                registrations[camera.Id] = registrations.GetValueOrDefault(camera.Id) + 1;
                if (camera.Slot >= 0)
                {
                    dockings[camera.Id] = dockings.GetValueOrDefault(camera.Id) + 1;
                }
            }
        }

        // Cameras no room has a record for keep their state on their own entity.
        CameraView[] looseCameras = entityRegistry.GetEntities<WorldEntity>()
                                                  .Where(entity => mapRoomCameraTechType.Equals(entity.TechType) && !registrations.ContainsKey(entity.Id))
                                                  .OrderBy(entity => entity.Id.ToString(), StringComparer.Ordinal)
                                                  .Select(ReadLooseCamera)
                                                  .ToArray();
        foreach (KeyValuePair<NitroxId, int> registration in registrations.Where(pair => pair.Value > 1))
        {
            anomalies.Add($"camera {ShortCamera(registration.Key)} is registered with {registration.Value} rooms");
        }
        foreach (KeyValuePair<NitroxId, int> docking in dockings.Where(pair => pair.Value > 1))
        {
            anomalies.Add($"camera {ShortCamera(docking.Key)} is docked in {docking.Value} rooms");
        }
        Dictionary<NitroxId, CameraLocks> locksByCamera = [];
        foreach (CameraView camera in roomViews.SelectMany(view => view.Cameras).Concat(looseCameras))
        {
            if (!locksByCamera.ContainsKey(camera.Id))
            {
                locksByCamera.Add(camera.Id, ReadCameraLocks(camera, anomalies));
            }
        }

        ScannerRoomDiagnosticCounters counters = diagnostics.GetCounters();
        IReadOnlyList<ScannerRoomDiagnosticEntry> history = diagnostics.GetHistory();
        int registeredCameras = registrations.Count;

        StringBuilder output = new();
        output.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Scanner Room diagnostics ep={diagnostics.Epoch}: rooms={rooms.Length}, cameras={registeredCameras + looseCameras.Length} (registered={registeredCameras}, loose={looseCameras.Length}), events={counters.Recorded} (accepted={counters.Accepted}, rejected={counters.Rejected}, invariant={counters.InvariantFailures}, checkpoints={counters.Checkpoints}), rejections without a log line={counters.SuppressedWarnings}, unchanged repeats skipped={diagnostics.UnchangedRepeats}, diagnostics failures={diagnostics.OwnFailures}"));
        output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Anomalies ({anomalies.Count}):"));
        foreach (string anomaly in anomalies)
        {
            output.AppendLine($" ! {anomaly}");
        }

        output.AppendLine("Rooms and their cameras (lock 2x = session 2 exclusively, 1t = session 1 transiently, none = nobody; control = session piloting):");
        foreach (RoomView view in roomViews)
        {
            ScannerRoomStateSnapshot snapshot = view.Snapshot;
            output.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{ScannerRoomDiagnostics.PREFIX} snapshot ep={diagnostics.Epoch} side=S room={view.Id} roomLock={diagnostics.DescribeLock(view.Id)} left={ShortCamera(snapshot.LeftDockCameraId)} right={ShortCamera(snapshot.RightDockCameraId)} dockRev={snapshot.DockingRevision} cams={snapshot.CameraCount} target={(string.IsNullOrEmpty(snapshot.ScanTarget) ? DiagnosticLineFormat.NOT_APPLICABLE : snapshot.ScanTarget)} nodes={snapshot.NodesScanned} metaGen={snapshot.MetadataGeneration} metaRev={snapshot.MetadataRevision} scanGen={snapshot.ScanResultGeneration} scanRev={snapshot.ScanResultRevision} results={snapshot.ScanResultCount} types={snapshot.ScanTypeCount} typesRev={snapshot.ScanTypesRevision} crafting={(snapshot.FabricatorActive ? 1 : 0)} fp={snapshot.Fingerprint}"));
            foreach (CameraView camera in view.Cameras)
            {
                output.AppendLine(FormatCamera(camera, locksByCamera[camera.Id]));
            }
        }
        output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Cameras no room has a record for ({looseCameras.Length}):"));
        foreach (CameraView camera in looseCameras)
        {
            output.AppendLine(FormatCamera(camera, locksByCamera[camera.Id]));
        }

        List<(ScannerRoomDiagnosticEntry Last, long First, int Count)> recent = FoldRepeats(history);
        int shown = Math.Min(recent.Count, RECENT_ENTRY_LIMIT);
        output.Append(string.Create(CultureInfo.InvariantCulture,
            $"Recent transitions (newest last, {shown} of the {history.Count} kept, repeats folded):"));
        foreach ((ScannerRoomDiagnosticEntry last, long first, int count) in recent.Skip(recent.Count - shown))
        {
            output.AppendLine();
            output.Append(string.Create(CultureInfo.InvariantCulture, $"{last.Time.UtcDateTime:HH:mm:ss}Z {last.Format()}"));
            if (count > 1)
            {
                output.Append(string.Create(CultureInfo.InvariantCulture, $" (x{count} since n={first})"));
            }
        }
        await context.ReplyAsync(output.ToString());
    }

    /// <summary>
    ///     A request that is rejected on every packet fills the history with copies of one entry. Folding runs of the
    ///     same entry keeps the last few real transitions on screen.
    /// </summary>
    internal static List<(ScannerRoomDiagnosticEntry Last, long First, int Count)> FoldRepeats(IReadOnlyList<ScannerRoomDiagnosticEntry> history)
    {
        List<(ScannerRoomDiagnosticEntry Last, long First, int Count)> folded = [];
        foreach (ScannerRoomDiagnosticEntry entry in history)
        {
            if (folded.Count > 0 && IsRepeat(folded[^1].Last, entry))
            {
                folded[^1] = (entry, folded[^1].First, folded[^1].Count + 1);
            }
            else
            {
                folded.Add((entry, entry.Sequence, 1));
            }
        }
        return folded;
    }

    /// <summary>
    ///     Reads a room and its camera records from a copy the Scanner Room manager took inside its gate, the gate the
    ///     packet processors change them in, so the docks, revisions, records and fingerprint of one room belong to the
    ///     same moment.
    /// </summary>
    private static RoomView ReadRoom(MapRoomEntity room)
    {
        ScannerRoomStateSnapshot snapshot = ScannerRoomStateFingerprint.Create(room);
        List<CameraView> cameras = [];
        foreach (MapRoomCameraRecord? record in room.CameraRegistry ?? [])
        {
            if (record?.CameraId == null)
            {
                continue;
            }
            int slot = record.CameraId == snapshot.LeftDockCameraId ? 0 : record.CameraId == snapshot.RightDockCameraId ? 1 : -1;
            cameras.Add(new CameraView(record.CameraId, record.CameraNumber, slot, record.LightOn, record.LightRevision,
                record.Energy, record.Health, record.ComponentRevision));
        }
        return new RoomView(room.Id, snapshot, ScannerRoomStateFingerprint.Validate(room), [.. cameras.OrderBy(camera => camera.Number)]);
    }

    /// <summary>
    ///     A camera without a room record keeps its state on its entity as one object that is replaced as a whole, so
    ///     one read of it is one moment.
    /// </summary>
    private static CameraView ReadLooseCamera(WorldEntity camera)
    {
        MapRoomCameraMetadata state = camera.Metadata as MapRoomCameraMetadata ?? new MapRoomCameraMetadata();
        return new CameraView(camera.Id, 0, -1, state.LightOn, state.LightRevision, state.Energy, state.Health, state.ComponentRevision);
    }

    private CameraLocks ReadCameraLocks(CameraView camera, List<string> anomalies)
    {
        bool hasLock = simulationOwnershipData.TryGetLock(camera.Id, out SimulationOwnershipData.PlayerLock playerLock);
        bool inWorld = entityRegistry.TryGetEntityById(camera.Id, out WorldEntity worldEntity) && mapRoomCameraTechType.Equals(worldEntity.TechType);
        bool hasController = scannerRoomManager.TryGetController(camera.Id, out SessionId controller);
        bool holderControls = hasLock && playerLock.LockType == SimulationLockType.EXCLUSIVE &&
                              hasController && controller == playerLock.Player.SessionId;
        string control = holderControls ? DiagnosticLineFormat.Session(playerLock.Player.SessionId) : DiagnosticLineFormat.NOT_APPLICABLE;
        string where = camera.Slot >= 0 ? string.Create(CultureInfo.InvariantCulture, $"slot{camera.Slot}") : inWorld ? "world" : "away";

        if (hasLock && playerLock.LockType == SimulationLockType.EXCLUSIVE && !holderControls)
        {
            anomalies.Add($"camera {ShortCamera(camera.Id)} is locked exclusively by {playerLock.Player.Name} #{playerLock.Player.SessionId} who is not piloting it: a Stalker carrying it, or a lock that was never released (a player who takes control of the camera takes this lock over)");
        }
        if (!holderControls && hasController)
        {
            control = "stale";
            anomalies.Add($"camera {ShortCamera(camera.Id)} has a pilot on record who does not hold its exclusive lock");
        }
        if (!hasLock && camera.Slot < 0 && inWorld)
        {
            anomalies.Add($"camera {ShortCamera(camera.Id)} is out in the world and nobody simulates it: it does not move, drain or report damage");
        }
        return new CameraLocks(diagnostics.DescribeLock(camera.Id), control, where);
    }

    private static string FormatCamera(CameraView camera, CameraLocks locks) => string.Create(CultureInfo.InvariantCulture,
        $"  cam={ShortCamera(camera.Id)} number={camera.Number} where={locks.Where} camLock={locks.Lock} control={locks.Control} light={(camera.LightOn ? "on" : "off")} lightRev={camera.LightRevision} energy={camera.Energy:F1} health={camera.Health:F1} compRev={camera.ComponentRevision} id={camera.Id}");

    private static bool IsRepeat(ScannerRoomDiagnosticEntry previous, ScannerRoomDiagnosticEntry next) =>
        previous.EventName == next.EventName && previous.Outcome == next.Outcome && previous.SessionId == next.SessionId &&
        previous.RoomId == next.RoomId && previous.CameraId == next.CameraId && previous.Slot == next.Slot && previous.Reason == next.Reason;

    private static string ShortRoom(NitroxId? id) => DiagnosticLineFormat.ShortId(id, false);

    private static string ShortCamera(NitroxId? id) => DiagnosticLineFormat.ShortId(id, true);

    private readonly record struct RoomView(NitroxId Id, ScannerRoomStateSnapshot Snapshot, string? Violation, CameraView[] Cameras);

    private readonly record struct CameraView(NitroxId Id, int Number, int Slot, bool LightOn, long LightRevision, float Energy, float Health,
        long ComponentRevision);

    private readonly record struct CameraLocks(string Lock, string Control, string Where);
}
