using System.ComponentModel;
using System.Linq;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Server.Subnautica.Models.Commands;

[RequiresPermission(Perms.ADMIN)]
internal sealed class ScannerMarkCommand(EntityRegistry entityRegistry, ScannerRoomDiagnostics diagnostics) : ICommandHandler<string>
{
    [Description("Writes a compact Scanner Room checkpoint to the server log, for example: scannermark D1-before-restart")]
    public async Task Execute(ICommandContext context, [Description("short acceptance-test checkpoint label")] string label)
    {
        MapRoomEntity[] rooms = entityRegistry.GetEntities<MapRoomEntity>()
                                             .OrderBy(room => room.Id.ToString(), StringComparer.Ordinal)
                                             .ToArray();
        if (rooms.Length == 0)
        {
            await context.ReplyAsync("No Scanner Rooms are registered; checkpoint was not written.");
            return;
        }

        string[] fingerprints = new string[rooms.Length];
        for (int index = 0; index < rooms.Length; index++)
        {
            MapRoomEntity room = rooms[index];
            ScannerRoomDiagnosticEntry entry = diagnostics.RecordCheckpoint("manual", room, label);
            fingerprints[index] = $"{room.Id}={entry.StateFingerprint}";
        }
        await context.ReplyAsync($"Scanner checkpoint '{label}' recorded for {rooms.Length} room(s): {string.Join(", ", fingerprints)}");
    }
}
