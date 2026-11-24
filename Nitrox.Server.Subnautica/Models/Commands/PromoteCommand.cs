using System.ComponentModel;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Commands.Core;

namespace Nitrox.Server.Subnautica.Models.Commands;

[RequiresPermission(Perms.MODERATOR)]
internal sealed class PromoteCommand : ICommandHandler<Player, Perms>
{
    [Description("Sets specific permissions to a user")]
    public async Task Execute(ICommandContext context, [Description("The username to change the permissions of")] Player targetPlayer, [Description("Permission level")] Perms newPerms)
    {
        if (context.OriginId == targetPlayer.SessionId)
        {
            await context.ReplyAsync("You can't promote yourself");
            return;
        }
        if (context.Permissions < newPerms)
        {
            await context.ReplyAsync($"Your permissions ({context.Permissions}) must be at least the perms you want to assign ({newPerms})");
            return;
        }
        // Like deop: a player of the same rank is out of reach, so moderators cannot strip each other.
        if (context.Permissions <= targetPlayer.Permissions)
        {
            await context.ReplyAsync($"You're not allowed to update {targetPlayer.Name}\'s permissions");
            return;
        }

        targetPlayer.Permissions = newPerms;
        await context.SendAsync(targetPlayer.SessionId, new PermsChanged(newPerms));
        await context.ReplyAsync($"Updated {targetPlayer.Name}\'s permissions to {newPerms}");
        await context.SendAsync(targetPlayer.SessionId, $"You've been promoted to {newPerms}");
    }
}
