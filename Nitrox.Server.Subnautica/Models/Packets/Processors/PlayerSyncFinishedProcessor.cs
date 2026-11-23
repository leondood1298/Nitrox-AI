using Nitrox.Server.Subnautica.Models.Communication;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Services;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal class PlayerSyncFinishedProcessor(SessionManager sessionManager, JoiningManager joiningManager, HibernateService hibernateService, BedOccupancy bedOccupancy)
    : IAuthPacketProcessor<PlayerSyncFinished>
{
    private readonly SessionManager sessionManager = sessionManager;
    private readonly JoiningManager joiningManager = joiningManager;
    private readonly HibernateService hibernateService = hibernateService;

    public async Task Process(AuthProcessorContext context, PlayerSyncFinished packet)
    {
        if (sessionManager.GetSessionCount() > 0)
        {
            await hibernateService.WakeAsync();
        }

        joiningManager.SyncFinishedCallback?.Invoke();

        // The world and the other players exist on the joining client now, so it can show who is lying in bed.
        foreach (BedEnterAnimation lyingDown in bedOccupancy.GetAnnouncementsFor(context.Sender.SessionId))
        {
            await context.ReplyAsync(lyingDown);
        }
    }
}
