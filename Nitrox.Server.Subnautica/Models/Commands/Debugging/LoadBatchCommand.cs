#if DEBUG
using System.ComponentModel;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Server.Subnautica.Models.Commands.Debugging;

[RequiresPermission(Perms.ADMIN)]
internal sealed class LoadBatchCommand(WorldEntityManager worldEntityManager) : ICommandHandler<int, int, int>
{
    private readonly WorldEntityManager worldEntityManager = worldEntityManager;

    [Description("Loads entities at x y z")]
    public async Task Execute(ICommandContext context, int xCoordinate, int yCoordinate, int zCoordinate)
    {
        NitroxInt3 batchId = new(xCoordinate, yCoordinate, zCoordinate);
        // Through the world, so the batch is filled once and its entities are kept like any other's
        int count = await worldEntityManager.LoadUnspawnedEntitiesAsync(batchId, false);

        await context.ReplyAsync($"Loaded {count} entities from batch {batchId}");
    }
}
#endif
