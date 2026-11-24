using System.ComponentModel;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Commands.Core;

namespace Nitrox.Server.Subnautica.Models.Commands;

[RequiresPermission(Perms.HOST)]
internal sealed class ChangeAdminPasswordCommand(IOptions<SubnauticaServerOptions> options, ILogger<ChangeAdminPasswordCommand> logger) : ICommandHandler<string>
{
    private readonly IOptions<SubnauticaServerOptions> options = options;
    private readonly ILogger<ChangeAdminPasswordCommand> logger = logger;

    [Description("Changes the password of /login (any text, the rest of the line); kept from the next save on")]
    public async Task Execute(ICommandContext context, [Description("The new admin password")] string newPassword)
    {
        options.Value.AdminPassword = newPassword;
        // The password itself never goes to the log, which is kept and often shared
        logger.ZLogInformation($"Admin password changed by {context.OriginName}");
        await context.ReplyAsync("Admin password has been updated");
    }
}
