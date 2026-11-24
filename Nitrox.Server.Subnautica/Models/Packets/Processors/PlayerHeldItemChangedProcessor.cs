using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class PlayerHeldItemChangedProcessor(PlayerHandAndSeatStates handAndSeatStates) : IAuthPacketProcessor<PlayerHeldItemChanged>
{
    private readonly PlayerHandAndSeatStates handAndSeatStates = handAndSeatStates;

    public async Task Process(AuthProcessorContext context, PlayerHeldItemChanged packet)
    {
        // The others would change whichever player the packet names
        if (packet.SessionId != context.Sender.SessionId)
        {
            return;
        }

        if (packet.IsFirstTime != null && !context.Sender.UsedItems.Contains(packet.IsFirstTime))
        {
            context.Sender.UsedItems.Add(packet.IsFirstTime);
        }
        handAndSeatStates.OnHeldItemChanged(context.Sender.SessionId, packet);

        await context.SendToOthersAsync(packet);
    }
}
