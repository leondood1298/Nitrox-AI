using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class PlayerStatsProcessor(ILogger<PlayerStatsProcessor> logger) : IAuthPacketProcessor<PlayerStats>
{
    private readonly ILogger<PlayerStatsProcessor> logger = logger;

    public async Task Process(AuthProcessorContext context, PlayerStats packet)
    {
        // Before its snapshot is applied, a game reports the stats of a fresh player; they would replace the stored ones
        if (!context.Sender.HasFinishedInitialSync)
        {
            return;
        }
        if (packet.SessionId != context.Sender.SessionId)
        {
            logger.ZLogWarningOnce($"Player ID mismatch (received: {packet.SessionId}, real: {context.Sender.SessionId})");
            packet.SessionId = context.Sender.SessionId;
        }
        context.Sender.Stats = new PlayerStatsData(packet.Oxygen, packet.MaxOxygen, packet.Health, packet.Food, packet.Water, packet.InfectionAmount);
        await context.SendToOthersAsync(packet);
    }
}
