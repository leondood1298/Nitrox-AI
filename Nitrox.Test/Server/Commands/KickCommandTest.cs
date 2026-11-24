using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Nitrox.Model.Configuration;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Administration;
using Nitrox.Server.Subnautica.Models.Commands;
using Nitrox.Server.Subnautica.Models.Commands.ArgConverters;
using Nitrox.Server.Subnautica.Models.Commands.ArgConverters.Core;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Test.Server.Helper;
using NSubstitute;

namespace Nitrox.Test.Server.Commands;

/// <summary>
///     A player waiting in the join queue can be kicked by name too.
/// </summary>
[TestClass]
public sealed class KickCommandTest
{
    [TestMethod]
    public async Task AQueuedPlayerIsFoundAndKicked()
    {
        ConnectedPlayers players = new();
        SessionId queued = players.Reserve("Queued", "key", out _);
        IKickPlayer kicker = Substitute.For<IKickPlayer>();
        kicker.KickPlayer(Arg.Any<SessionId>(), Arg.Any<string>()).Returns(true);

        ConvertResult found = await new PlayerNameToReservedSessionArgConverter(players.PlayerManager).ConvertAsync("Queued");
        found.Success.Should().BeTrue();
        await new KickCommand(kicker, players.PlayerManager).Execute(new RecordingCommandContext(), (ReservedSession)found.Value!, "go");

        await kicker.Received(1).KickPlayer(queued, "go");
    }

    [TestMethod]
    public async Task AConnectedPlayerIsNotTakenForAQueuedOne()
    {
        ConnectedPlayers players = new();
        players.Connect("InGame");

        ConvertResult found = await new PlayerNameToReservedSessionArgConverter(players.PlayerManager).ConvertAsync("InGame");

        found.Success.Should().BeFalse();
    }

    [TestMethod]
    public async Task AModeratorCannotKickAQueuedPlayerWhoWouldJoinAsModerator()
    {
        ConnectedPlayers players = new(options: new SubnauticaServerOptions { DefaultPlayerPerm = Perms.MODERATOR, LocalhostIsAdmin = false });
        players.Reserve("Queued", "key", out _);
        IKickPlayer kicker = Substitute.For<IKickPlayer>();
        ConvertResult found = await new PlayerNameToReservedSessionArgConverter(players.PlayerManager).ConvertAsync("Queued");
        RecordingCommandContext moderator = new() { Origin = CommandOrigin.PLAYER, OriginId = (SessionId)50, Permissions = Perms.MODERATOR };

        await new KickCommand(kicker, players.PlayerManager).Execute(moderator, (ReservedSession)found.Value!, "");

        await kicker.DidNotReceiveWithAnyArgs().KickPlayer(default, default);
    }

    [TestMethod]
    public async Task AModeratorCannotKickAQueuedAdminWhoseRankIsKept()
    {
        ConnectedPlayers players = new(options: new SubnauticaServerOptions { LocalhostIsAdmin = false });
        Nitrox.Server.Subnautica.Models.Player admin = players.Connect("Admin", "admins-key");
        Nitrox.Server.Subnautica.Models.GameLogic.PlayerRanks.Grant(admin, Perms.ADMIN);
        players.Disconnect(admin.SessionId);
        players.Reserve("Admin", "admins-key", out _);
        IKickPlayer kicker = Substitute.For<IKickPlayer>();
        ConvertResult found = await new PlayerNameToReservedSessionArgConverter(players.PlayerManager).ConvertAsync("Admin");
        RecordingCommandContext moderator = new() { Origin = CommandOrigin.PLAYER, OriginId = (SessionId)50, Permissions = Perms.MODERATOR };

        await new KickCommand(kicker, players.PlayerManager).Execute(moderator, (ReservedSession)found.Value!, "");

        await kicker.DidNotReceiveWithAnyArgs().KickPlayer(default, default);
    }
}
