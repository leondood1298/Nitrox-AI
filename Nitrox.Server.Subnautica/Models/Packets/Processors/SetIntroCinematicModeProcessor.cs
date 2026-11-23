using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class SetIntroCinematicModeProcessor(IntroCinematicPairing introCinematicPairing, ILogger<SetIntroCinematicModeProcessor> logger) : IAuthPacketProcessor<SetIntroCinematicMode>
{
    private readonly IntroCinematicPairing introCinematicPairing = introCinematicPairing;
    private readonly ILogger<SetIntroCinematicModeProcessor> logger = logger;

    public async Task Process(AuthProcessorContext context, SetIntroCinematicMode packet)
    {
        if (packet.SessionId != context.Sender.SessionId)
        {
            logger.ZLogWarning($"Received packet where {nameof(SetIntroCinematicMode.SessionId)} #{packet.SessionId} was not equal to sending {nameof(SetIntroCinematicMode.SessionId)} #{context.Sender.SessionId}");
            return;
        }

        // A player whose intro is over could otherwise be paired with a new player, who would then wait for an intro
        // that never starts on the other side.
        IntroCinematicMode current = context.Sender.PlayerContext.IntroCinematicMode;
        if (!IntroCinematicPairing.IsTransitionAllowed(current, packet.Mode))
        {
            logger.ZLogDebug($"Ignored IntroCinematicMode {packet.Mode} from {context.Sender.PlayerContext.PlayerName}, whose intro is {current}");
            return;
        }

        packet.PartnerId = null; // Resetting incoming packets just to be safe we don't relay any PartnerId. Server has only authority.
        context.Sender.PlayerContext.IntroCinematicMode = packet.Mode;
        await context.SendToOthersAsync(packet);
        logger.ZLogDebug($"IntroCinematicMode set to {packet.Mode} for {context.Sender.PlayerContext.PlayerName}");

        // The player chose to play alone at the moment the server paired it; its partner would start the intro with it
        if (current == IntroCinematicMode.START && packet.Mode == IntroCinematicMode.SINGLEPLAYER)
        {
            await introCinematicPairing.ReleasePartnerAsync(context.Sender.SessionId);
        }
        await introCinematicPairing.PairWaitingPlayersAsync();
    }
}
