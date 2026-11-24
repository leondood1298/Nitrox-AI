using System.Reflection;
using System.Threading.Tasks;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Commands;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Test.Server.Helper;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.Commands;

/// <summary>
///     A plain player could strip others of every command with <c>promote</c>, and moderators could strip each other.
/// </summary>
[TestClass]
public sealed class PromoteCommandTest
{
    [TestMethod]
    public void PlainPlayersCannotRunIt()
    {
        typeof(PromoteCommand).GetCustomAttribute<RequiresPermissionAttribute>()!.MinimumPermission.Should().Be(Perms.MODERATOR);
    }

    [TestMethod]
    public async Task AModeratorCannotChangeAnotherModerator()
    {
        ServerPlayer target = new ConnectedPlayers().Connect("Target");
        target.Permissions = Perms.MODERATOR;
        RecordingCommandContext context = new() { Origin = CommandOrigin.PLAYER, OriginId = (SessionId)99, Permissions = Perms.MODERATOR };

        await new PromoteCommand().Execute(context, target, Perms.NONE);

        target.Permissions.Should().Be(Perms.MODERATOR);
    }

    [TestMethod]
    public async Task AModeratorCanPromoteAPlayerToModerator()
    {
        ServerPlayer target = new ConnectedPlayers().Connect("Target");
        target.Permissions = Perms.PLAYER;
        RecordingCommandContext context = new() { Origin = CommandOrigin.PLAYER, OriginId = (SessionId)99, Permissions = Perms.MODERATOR };

        await new PromoteCommand().Execute(context, target, Perms.MODERATOR);

        target.Permissions.Should().Be(Perms.MODERATOR);
    }
}
