using Nitrox.Model.Core;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class PlayerDeathEventProcessor(PlayerDeaths playerDeaths) : IAuthPacketProcessor<PlayerDeathEvent>
{
    internal const string HARDCORE_DEATH_KICK_REASON = "You died in hardcore mode. This world can no longer be joined with this name; what you carried is lost.";

    private readonly PlayerDeaths playerDeaths = playerDeaths;

    public async Task Process(AuthProcessorContext context, PlayerDeathEvent packet)
    {
        // The others are told who died; never another player than the sender
        PlayerDeathEvent death = new(context.Sender.SessionId, packet.DeathPosition);
        bool permanent = playerDeaths.Died(context.Sender, packet.DeathPosition);
        await context.SendToOthersAsync(death);

        if (permanent)
        {
            // The kicked game disconnects itself; the reservation of this name is refused from now on
            await context.ReplyAsync(new PlayerKicked(HARDCORE_DEATH_KICK_REASON));
            return;
        }
        if (context.Sender.Permissions > Perms.MODERATOR)
        {
            await context.ReplyAsync(new ChatMessage(SessionId.SERVER_ID, "You can use /back to go to your death location"));
        }
    }
}
