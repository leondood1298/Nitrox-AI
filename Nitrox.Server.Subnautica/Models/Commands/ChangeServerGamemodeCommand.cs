using System.ComponentModel;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Commands;

[RequiresPermission(Perms.ADMIN)]
internal sealed class ChangeServerGamemodeCommand(PlayerManager playerManager, IOptions<SubnauticaServerOptions> serverConfig) : ICommandHandler<SubnauticaGameMode>
{
    private readonly IOptions<SubnauticaServerOptions> serverConfig = serverConfig;
    private readonly PlayerManager playerManager = playerManager;

    [Description("Changes the game mode of the server and of every player, also offline ones")]
    public async Task Execute(ICommandContext context, [Description("Gamemode to change to")] SubnauticaGameMode newGameMode)
    {
        // Applied even if the server's mode is the same already: single players may have been given another one
        serverConfig.Value.GameMode = newGameMode;
        foreach (Player player in playerManager.GetAllPlayers())
        {
            player.SetGameMode(newGameMode);
        }
        await context.SendToAllAsync(GameModeChanged.ForAllPlayers(newGameMode));
        await context.SendToAllAsync($"Server gamemode changed to \"{newGameMode}\" by {context.OriginName}");
    }
}
