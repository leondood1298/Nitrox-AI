using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Logs the game's developer console commands. The game enforces who may use them (ConsoleCommandPolicy, admin); this
///     only records a player below that rank who tried.
/// </summary>
internal sealed class CheatCommandProcessor(ILogger<CheatCommandProcessor> logger) : IAuthPacketProcessor<CheatCommand>
{
    private const Perms CONSOLE_PERMS = Perms.ADMIN;

    public async Task Process(AuthProcessorContext context, CheatCommand packet)
    {
        if (context.Sender.Permissions < CONSOLE_PERMS)
        {
            logger.ZLogWarning($"{context.Sender.Name} used cheat command: '{packet.Command}' without sufficient permissions.");
            return;
        }

        logger.ZLogInformation($"{context.Sender.Name} used cheat command: '{packet.Command}'");
    }
}
