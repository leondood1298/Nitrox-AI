using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class PlayerDeathProcessor(PlayerManager playerManager) : IClientPacketProcessor<PlayerDeathEvent>
{
    private readonly PlayerManager playerManager = playerManager;

    public Task Process(ClientProcessorContext context, PlayerDeathEvent playerDeath)
    {
        if (!playerManager.TryFind(playerDeath.SessionId, out RemotePlayer player))
        {
            Log.Warn($"Death of unknown session #{playerDeath.SessionId}");
            return Task.CompletedTask;
        }
        Log.Debug($"{player.PlayerName} died");
        Log.InGame(Language.main.Get("Nitrox_PlayerDied").Replace("{PLAYER}", player.PlayerName));
        player.PlayerDeathEvent.Trigger(player);
        return Task.CompletedTask;
    }
}
