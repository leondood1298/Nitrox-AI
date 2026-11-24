using Nitrox.Server.Subnautica.Models.Commands.ArgConverters.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Server.Subnautica.Models.Commands.ArgConverters;

/// <summary>
///     A player who has joined this world, online or not, for commands whose effect lasts (rank, mute, ban).
/// </summary>
internal sealed record KnownPlayer(Player Player)
{
    public string Name => Player.Name;
}

/// <summary>
///     Converts a player name to any player who has joined this world under it, also one who is offline.
/// </summary>
internal sealed class PlayerNameToKnownPlayerArgConverter(PlayerManager playerManager) : IArgConverter<string, KnownPlayer>
{
    private readonly PlayerManager playerManager = playerManager;

    public Task<ConvertResult> ConvertAsync(string playerName)
    {
        if (!playerManager.TryGetKnownPlayerByName(playerName, out Player? player))
        {
            return Task.FromResult(ConvertResult.Fail($"No player named '{playerName}' has joined this world"));
        }
        return Task.FromResult(ConvertResult.Ok(new KnownPlayer(player)));
    }
}

/// <summary>
///     Converts the session id of an online player, as the other player arguments accept it too.
/// </summary>
internal sealed class SessionIdToKnownPlayerArgConverter(PlayerManager playerManager) : IArgConverter<ushort, KnownPlayer>
{
    private readonly PlayerManager playerManager = playerManager;

    public Task<ConvertResult> ConvertAsync(ushort sessionId)
    {
        if (sessionId < 1 || !playerManager.TryGetPlayerBySessionId(sessionId, out Player? player))
        {
            return Task.FromResult(ConvertResult.Fail($"No player found by session #{sessionId}"));
        }
        return Task.FromResult(ConvertResult.Ok(new KnownPlayer(player)));
    }
}
