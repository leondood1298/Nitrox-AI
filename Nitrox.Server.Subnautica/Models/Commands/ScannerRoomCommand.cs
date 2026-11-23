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

    [Description("Lists Scanner Room totals and anomalies, then every room and camera with its locks, then the recent transitions")]
    public async Task Execute(ICommandContext context)
    {
        // Copies of all rooms from one moment, ordered by id. Nothing below can change what the server holds.
        IReadOnlyList<ScannerRoomSnapshot> rooms = scannerRoomManager.GetSnapshots();
        List<string> anomalies = [];
        List<RoomView> roomViews = [];
        Dictionary<NitroxId, int> dockings = [];
        foreach (ScannerRoomSnapshot captured in rooms)
        {
            MapRoomEntity room = captured.Room;
            RoomView view = ReadRoom(captured);
            roomViews.Add(view);
            if (view.Violation != null)
            {
                anomalies.Add($"room {ShortRoom(room.Id)} breaks an invariant: {view.Violation}");
            }
            if (!simulationOwnershipData.TryGetLock(room.Id, out _))
            {
                anomalies.Add($"room {ShortRoom(room.Id)} has no simulating player: scan results and scan types are only accepted from that player");
            }
            foreach (CameraView camera in view.Cameras)
            {
                dockings[camera.Id] = dockings.GetValueOrDefault(camera.Id) + 1;
            }
        }

        // The cameras that are out in the world and in no dock.
        CameraView[] looseCameras = entityRegistry.GetEntities<WorldEntity>()
                                                  .Where(entity => ScannerRoomManager.IsCamera(entity) && !dockings.ContainsKey(entity.Id))
                                                  .OrderBy(entity => entity.Id.ToString(), StringComparer.Ordinal)
                                                  .Select(camera => ReadCamera(camera, MapRoomCameraMetadata.NOT_DOCKED))
                                                  .ToArray();
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
        int dockedCameras = dockings.Count;

        StringBuilder output = new();
        output.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Scanner Room diagnostics ep={diagnostics.Epoch}: rooms={rooms.Count}, cameras={dockedCameras + looseCameras.Length} (docked={dockedCameras}, loose={looseCameras.Length}), events={counters.Recorded} (accepted={counters.Accepted}, rejected={counters.Rejected}, invariant={counters.InvariantFailures}, checkpoints={counters.Checkpoints}), rejections without a log line={counters.SuppressedWarnings}, unchanged repeats skipped={diagnostics.UnchangedRepeats}, diagnostics failures={diagnostics.OwnFailures}"));
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
                $"{ScannerRoomDiagnostics.PREFIX} snapshot ep={diagnostics.Epoch} side=S room={view.Id} roomLock={diagnostics.DescribeLock(view.Id)} left={ShortCamera(snapshot.LeftDockCameraId)} right={ShortCamera(snapshot.RightDockCameraId)} cams={snapshot.CameraCount} target={(string.IsNullOrEmpty(snapshot.ScanTarget) ? DiagnosticLineFormat.NOT_APPLICABLE : snapshot.ScanTarget)} nodes={snapshot.NodesScanned} metaGen={snapshot.MetadataGeneration} results={view.ScanResultCount} types={view.ScanTypeCount} crafting={(snapshot.FabricatorActive ? 1 : 0)} fp={snapshot.Fingerprint}"));
            foreach (CameraView camera in view.Cameras)
            {
                output.AppendLine(FormatCamera(camera, locksByCamera[camera.Id]));
            }
        }
        output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Cameras in no dock ({looseCameras.Length}):"));
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
    ///     Reads a room and the cameras in its docks from a copy the Scanner Room manager took inside its gate, the gate
    ///     the packet processors change them in, so the cameras, their state and the fingerprint of one room belong to
    ///     the same moment.
    /// </summary>
    private static RoomView ReadRoom(ScannerRoomSnapshot captured)
    {
        MapRoomEntity room = captured.Room;
        ScannerRoomStateSnapshot snapshot = ScannerRoomStateFingerprint.Create(room);
        CameraView[] cameras = [.. ScannerRoomStateFingerprint.GetCameras(room).Select(camera => ReadCamera(camera, ScannerRoomStateFingerprint.ReadState(camera).DockingIndex))
                                                                .OrderBy(camera => camera.Slot)];
        return new RoomView(room.Id, snapshot, ScannerRoomStateFingerprint.Validate(room), cameras, captured.ScanResultCount, captured.ScanTypeCount);
    }

    /// <summary>
    ///     A camera keeps its state on its entity as one object that is replaced as a whole, so one read of it is one
    ///     moment.
    /// </summary>
    private static CameraView ReadCamera(Entity camera, int slot)
    {
        MapRoomCameraMetadata state = ScannerRoomStateFingerprint.ReadState(camera);
        return new CameraView(camera.Id, state.CameraNumber, slot, state.LightOn, state.Energy, state.Health);
    }

    private CameraLocks ReadCameraLocks(CameraView camera, List<string> anomalies)
    {
        bool hasLock = simulationOwnershipData.TryGetLock(camera.Id, out SimulationOwnershipData.PlayerLock playerLock);
        bool inWorld = entityRegistry.TryGetEntityById(camera.Id, out WorldEntity worldEntity) && ScannerRoomManager.IsCamera(worldEntity);
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
        $"  cam={ShortCamera(camera.Id)} number={camera.Number} where={locks.Where} camLock={locks.Lock} control={locks.Control} light={(camera.LightOn ? "on" : "off")} energy={camera.Energy:F1} health={camera.Health:F1} id={camera.Id}");

    private static bool IsRepeat(ScannerRoomDiagnosticEntry previous, ScannerRoomDiagnosticEntry next) =>
        previous.EventName == next.EventName && previous.Outcome == next.Outcome && previous.SessionId == next.SessionId &&
        previous.RoomId == next.RoomId && previous.CameraId == next.CameraId && previous.Slot == next.Slot && previous.Reason == next.Reason;

    private static string ShortRoom(NitroxId? id) => DiagnosticLineFormat.ShortId(id, false);

    private static string ShortCamera(NitroxId? id) => DiagnosticLineFormat.ShortId(id, true);

    private readonly record struct RoomView(NitroxId Id, ScannerRoomStateSnapshot Snapshot, string? Violation, CameraView[] Cameras, int ScanResultCount, int ScanTypeCount);

    private readonly record struct CameraView(NitroxId Id, int Number, int Slot, bool LightOn, float Energy, float Health);

    private readonly record struct CameraLocks(string Lock, string Control, string Where);
}
