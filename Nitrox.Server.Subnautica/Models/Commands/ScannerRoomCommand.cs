using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Server.Subnautica.Models.Commands;

[RequiresPermission(Perms.ADMIN)]
internal sealed class ScannerRoomCommand(EntityRegistry entityRegistry, ScannerRoomDiagnostics diagnostics) : ICommandHandler
{
    private const int RECENT_ENTRY_LIMIT = 25;

    [Description("Lists canonical Scanner Room fingerprints, compact counters, and recent transition diagnostics")]
    public async Task Execute(ICommandContext context)
    {
        MapRoomEntity[] rooms = entityRegistry.GetAllEntities()
                                              .OfType<MapRoomEntity>()
                                              .OrderBy(room => room.Id.ToString(), StringComparer.Ordinal)
                                              .ToArray();
        foreach (MapRoomEntity room in rooms)
        {
            if (ScannerRoomStateFingerprint.Validate(room) is { } violation)
            {
                diagnostics.RecordInvariantFailure("snapshot_invalid", room, reason: violation);
            }
        }
        ScannerRoomDiagnosticCounters counters = diagnostics.GetCounters();
        ScannerRoomDiagnosticEntry[] recent = diagnostics.GetHistory().TakeLast(RECENT_ENTRY_LIMIT).ToArray();

        StringBuilder output = new();
        output.Append("Scanner Room diagnostics: rooms=")
              .Append(rooms.Length.ToString(CultureInfo.InvariantCulture))
              .Append(", recorded=").Append(counters.Recorded.ToString(CultureInfo.InvariantCulture))
              .Append(", accepted=").Append(counters.Accepted.ToString(CultureInfo.InvariantCulture))
              .Append(", rejected=").Append(counters.Rejected.ToString(CultureInfo.InvariantCulture))
              .Append(", invariant=").Append(counters.InvariantFailures.ToString(CultureInfo.InvariantCulture))
              .Append(", checkpoints=").Append(counters.Checkpoints.ToString(CultureInfo.InvariantCulture))
              .Append(", warningsSuppressed=").Append(counters.SuppressedWarnings.ToString(CultureInfo.InvariantCulture))
              .AppendLine();

        foreach (MapRoomEntity room in rooms)
        {
            ScannerRoomStateSnapshot snapshot = ScannerRoomStateFingerprint.Create(room);
            output.Append(ScannerRoomDiagnostics.Prefix)
                  .Append(" snapshot ep=").Append(diagnostics.Epoch)
                  .Append(" side=S room=").Append(room.Id)
                  .Append(" left=").Append(ShortId(room.LeftDockCameraId))
                  .Append(" right=").Append(ShortId(room.RightDockCameraId))
                  .Append(" dRev=").Append(snapshot.DockingRevision.ToString(CultureInfo.InvariantCulture))
                  .Append(" cams=").Append(snapshot.CameraCount.ToString(CultureInfo.InvariantCulture))
                  .Append(" target=").Append(string.IsNullOrEmpty(snapshot.ScanTarget) ? "-" : snapshot.ScanTarget)
                  .Append(" nodes=").Append(snapshot.NodesScanned.ToString(CultureInfo.InvariantCulture))
                  .Append(" mGen=").Append(snapshot.MetadataGeneration.ToString(CultureInfo.InvariantCulture))
                  .Append(" mRev=").Append(snapshot.MetadataRevision.ToString(CultureInfo.InvariantCulture))
                  .Append(" results=").Append(snapshot.ScanResultCount.ToString(CultureInfo.InvariantCulture))
                  .Append(" types=").Append(snapshot.ScanTypeCount.ToString(CultureInfo.InvariantCulture))
                  .Append(" crafting=").Append(snapshot.FabricatorActive ? "1" : "0")
                  .Append(" fp=").Append(snapshot.Fingerprint)
                  .AppendLine();
        }

        output.Append("Recent Scanner Room transitions: ").Append(recent.Length.ToString(CultureInfo.InvariantCulture));
        foreach (ScannerRoomDiagnosticEntry entry in recent)
        {
            output.AppendLine().Append(entry.Format());
        }
        await context.ReplyAsync(output.ToString());
    }

    private static string ShortId(Nitrox.Model.DataStructures.NitroxId? id)
    {
        string value = id?.ToString() ?? "-";
        return value.Length > 8 ? value[..8] : value;
    }
}
