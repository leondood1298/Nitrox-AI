using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nitrox.Model.Configuration;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.AppEvents;
using Nitrox.Server.Subnautica.Models.Commands;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.Communication;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.Helper;
using NSubstitute;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.Commands;

/// <summary>
///     On a server whose default rank is NONE nobody could log in or ask for help, the admin password could be guessed
///     without limit, and a login did not outlast leaving.
/// </summary>
[TestClass]
public sealed class LoginCommandTest
{
    [TestMethod]
    public void LoginAndHelpWorkAtEveryRank()
    {
        typeof(LoginCommand).GetCustomAttribute<RequiresPermissionAttribute>()!.MinimumPermission.Should().Be(Perms.NONE);
        typeof(HelpCommand).GetCustomAttribute<RequiresPermissionAttribute>()!.MinimumPermission.Should().Be(Perms.NONE);
    }

    [TestMethod]
    public async Task ALoginIsKeptForTheNextJoin()
    {
        ConnectedPlayers players = new();
        ServerPlayer bob = players.Connect("Bob");
        LoginCommand login = new(Options.Create(new SubnauticaServerOptions { AdminPassword = "secret phrase" }), players.SessionManager);

        await login.Execute(new PlayerToServerCommandContext(Substitute.For<IPacketSender>(), bob), "secret phrase");

        bob.Permissions.Should().Be(Perms.ADMIN);
        bob.GrantedPermissions.Should().Be(Perms.ADMIN);
    }

    [TestMethod]
    public async Task GuessingIsSlowedDown()
    {
        ConnectedPlayers players = new();
        ServerPlayer bob = players.Connect("Bob");
        LoginCommand login = new(Options.Create(new SubnauticaServerOptions { AdminPassword = "secret" }), players.SessionManager);
        PlayerToServerCommandContext context = new(Substitute.For<IPacketSender>(), bob);

        for (int i = 0; i < 3; i++)
        {
            await login.Execute(context, "wrong");
        }
        await login.Execute(context, "secret");
        bob.Permissions.Should().Be(Perms.PLAYER, "the right password must wait until the wrong tries have cooled down");

        bob.GrantedPermissions.Should().BeNull();
    }
}
