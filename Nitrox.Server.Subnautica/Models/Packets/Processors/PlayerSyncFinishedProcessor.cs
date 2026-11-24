using Nitrox.Server.Subnautica.Models.Communication;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Services;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal class PlayerSyncFinishedProcessor(SessionManager sessionManager, JoiningManager joiningManager, HibernateService hibernateService, BedOccupancy bedOccupancy, PlayerAnimationStates animationStates,
    ScannerRoomManager scannerRoomManager, PlayerHandAndSeatStates handAndSeatStates, SoundLoops soundLoops)
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

        if (context.Sender.HasFinishedInitialSync)
        {
            // A game reports this once per join; a repeat would announce the player again
            return;
        }
        context.Sender.HasFinishedInitialSync = true;
        await joiningManager.SyncFinishedAsync(context.Sender);

        // The world and the other players exist on the joining client now, so it can show who is lying in bed.
        foreach (BedEnterAnimation lyingDown in bedOccupancy.GetAnnouncementsFor(context.Sender.SessionId))
        {
            await context.ReplyAsync(lyingDown);
        }
        // And who is holding up their PDA or using a tool.
        foreach (AnimationChangeEvent switchedOn in animationStates.GetAnnouncementsFor(context.Sender.SessionId))
        {
            await context.ReplyAsync(switchedOn);
        }
        // And what everyone holds and where they sit.
        foreach (Packet holdingOrSeated in handAndSeatStates.GetAnnouncementsFor(context.Sender.SessionId))
        {
            await context.ReplyAsync(holdingOrSeated);
        }
        // And who is controlling which Scanner Room camera.
        await scannerRoomManager.SendControlGrantsAsync(context.Sender.SessionId);
        // And which looping sounds play (a heated knife, a gun terminal): their start was announced before this player came.
        FMODActiveLoops activeLoops = soundLoops.GetActiveLoops();
        if (activeLoops.Loops.Count > 0)
        {
            await context.ReplyAsync(activeLoops);
        }
    }
}
