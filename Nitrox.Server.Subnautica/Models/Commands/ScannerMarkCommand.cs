using System.ComponentModel;
using System.Linq;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Helper;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Server.Subnautica.Models.Commands;

/// <summary>
///     Lets a tester bracket a test step in the server log. The marker is written for both the Scanner Room and the
///     base power diagnostics and does not depend on a Scanner Room existing, because a step usually starts before
///     anything is built.
/// </summary>
[RequiresPermission(Perms.ADMIN)]
internal sealed class ScannerMarkCommand(ScannerRoomManager scannerRoomManager, ScannerRoomDiagnostics diagnostics, BasePowerDiagnostics basePowerDiagnostics) : ICommandHandler<string>
{
    [Description("Writes a marker to the server log, plus a checkpoint with fingerprint per Scanner Room, for example: scannermark D1-before-restart")]
    public async Task Execute(ICommandContext context, [Description("one word naming the test step")] string label)
    {
        string token = DiagnosticLineFormat.Token(label, ScannerRoomDiagnostics.REASON_LENGTH);
        if (token == DiagnosticLineFormat.NOT_APPLICABLE)
        {
            await context.ReplyAsync("The label needs at least one letter or digit; nothing was written.");
            return;
        }

        diagnostics.RecordMark(token);
        basePowerDiagnostics.RecordMark(token);
        // Copies of all rooms from one moment, ordered by id.
        MapRoomEntity[] rooms = scannerRoomManager.GetSnapshots().Select(snapshot => snapshot.Room).ToArray();
        string[] fingerprints = new string[rooms.Length];
        for (int index = 0; index < rooms.Length; index++)
        {
            MapRoomEntity room = rooms[index];
            ScannerRoomDiagnosticEntry entry = diagnostics.RecordCheckpoint("manual", room, token);
            fingerprints[index] = $"{room.Id}={entry.StateFingerprint ?? DiagnosticLineFormat.NOT_APPLICABLE}";
        }
        string checkpoints = rooms.Length == 0 ? "no Scanner Rooms to checkpoint" : $"{rooms.Length} Scanner Room checkpoint(s): {string.Join(", ", fingerprints)}";
        await context.ReplyAsync($"Marked the server log with ev=mark reason={token} ({ScannerRoomDiagnostics.PREFIX} and {BasePowerDiagnostics.PREFIX}); {checkpoints}");
    }
}
