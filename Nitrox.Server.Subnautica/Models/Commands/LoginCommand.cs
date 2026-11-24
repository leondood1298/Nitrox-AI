using System.ComponentModel;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Administration;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.Communication;
using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Server.Subnautica.Models.Commands;

/// <remarks>
///     Usable at every rank, so that an admin can log in on a server whose default rank is <see cref="Perms.NONE" />.
/// </remarks>
[RequiresOrigin(CommandOrigin.PLAYER)]
[RequiresPermission(Perms.NONE)]
internal sealed class LoginCommand(IOptions<SubnauticaServerOptions> optionsProvider, SessionManager sessionManager) : ICommandHandler<string>
{
    private const Perms LOGIN_PERMS = Perms.ADMIN;

    private readonly IOptions<SubnauticaServerOptions> optionsProvider = optionsProvider;
    private readonly SessionManager sessionManager = sessionManager;
    private readonly GuessLimiter guesses = new();

    [Description("Log in to server as admin (requires password); kept when you join again")]
    public async Task Execute(ICommandContext context, [Description("The admin password for the server")] string adminPassword)
    {
        string? activePassword = optionsProvider.Value.AdminPassword;
        if (string.IsNullOrWhiteSpace(activePassword))
        {
            await context.ReplyAsync("Logging in with admin password is disabled");
            return;
        }

        switch (context)
        {
            case PlayerToServerCommandContext { Player: { Permissions: < LOGIN_PERMS } player }:
                string[] guessKeys = [$"address:{sessionManager.GetEndPoint(player.SessionId)?.Address}", $"session:{player.SessionId}", $"player:{player.Name}"];
                TimeSpan wait = guesses.GetRemainingWait(guessKeys);
                if (wait > TimeSpan.Zero)
                {
                    await context.ReplyAsync($"Too many wrong passwords. Try again in {Math.Ceiling(wait.TotalSeconds)} seconds.");
                    return;
                }
                if (SecretComparison.AreEqual(adminPassword, activePassword))
                {
                    guesses.RecordSuccess(guessKeys);
                    PlayerRanks.Grant(player, LOGIN_PERMS);
                    await context.ReplyAsync(new PermsChanged(LOGIN_PERMS));
                    await context.ReplyAsync($"You've been made {LOGIN_PERMS} on this server!");
                }
                else
                {
                    guesses.RecordFailure(guessKeys);
                    await context.ReplyAsync("Incorrect Password");
                }
                break;
            default:
                await context.ReplyAsync("You already have admin permissions");
                break;
        }
    }
}
