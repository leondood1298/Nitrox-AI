using Nitrox.Model.Core;
using Nitrox.Server.Subnautica.Models.Commands.ArgConverters.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Server.Subnautica.Models.Commands.ArgConverters;

/// <summary>
///     A player who holds a name but is not a player in the world yet: waiting in the join queue or loading the scene.
/// </summary>
internal sealed record ReservedSession(SessionId SessionId, string Name);

/// <summary>
///     Converts a player name to the session holding it while that player is queued, so that commands such as kick and
///     ban can reach a player before they are in the world.
/// </summary>
internal sealed class PlayerNameToReservedSessionArgConverter(PlayerManager playerManager) : IArgConverter<string, ReservedSession>
{
    private readonly PlayerManager playerManager = playerManager;

    public Task<ConvertResult> ConvertAsync(string playerName)
    {
        if (!playerManager.TryGetSessionByName(playerName, out SessionId sessionId) || !playerManager.IsReserved(sessionId))
        {
            return Task.FromResult(ConvertResult.Fail($"No queued player found by name '{playerName}'"));
        }
        return Task.FromResult(ConvertResult.Ok(new ReservedSession(sessionId, playerName)));
    }
}
