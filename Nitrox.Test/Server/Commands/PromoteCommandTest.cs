using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Nitrox.Model.Configuration;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Commands;
using Nitrox.Server.Subnautica.Models.Commands.ArgConverters;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Test.Server.Helper;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.Commands;

/// <summary>
///     A plain player could strip others of every command with <c>promote</c>, and moderators could strip each other.
/// </summary>
[TestClass]
public sealed class PromoteCommandTest
{
    private static readonly IOptions<SubnauticaServerOptions> defaults = Options.Create(new SubnauticaServerOptions());

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

        await new PromoteCommand(defaults).Execute(context, new KnownPlayer(target), Perms.NONE);

        target.Permissions.Should().Be(Perms.MODERATOR);
    }

    [TestMethod]
    public async Task AModeratorCanPromoteAPlayerToModerator()
    {
        ServerPlayer target = new ConnectedPlayers().Connect("Target");
        target.Permissions = Perms.PLAYER;
        RecordingCommandContext context = new() { Origin = CommandOrigin.PLAYER, OriginId = (SessionId)99, Permissions = Perms.MODERATOR };

        await new PromoteCommand(defaults).Execute(context, new KnownPlayer(target), Perms.MODERATOR);

        target.Permissions.Should().Be(Perms.MODERATOR);
        target.GrantedPermissions.Should().Be(Perms.MODERATOR, "the rank is kept for the next join");
    }

    [TestMethod]
    public async Task AnOfflinePlayerCanBePromoted()
    {
        ConnectedPlayers players = new();
        ServerPlayer target = players.Connect("Target");
        players.Disconnect(target.SessionId);

        await new PromoteCommand(defaults).Execute(new RecordingCommandContext(), new KnownPlayer(target), Perms.MODERATOR);

        target.GrantedPermissions.Should().Be(Perms.MODERATOR);
    }

    [TestMethod]
    public async Task OpDoesNotDemoteAHigherRank()
    {
        ServerPlayer host = new ConnectedPlayers().Connect("Host");
        host.Permissions = Perms.HOST;

        await new OpCommand(defaults).Execute(new RecordingCommandContext(), new KnownPlayer(host));

        host.Permissions.Should().Be(Perms.HOST);
    }

    [TestMethod]
    public async Task DeopGivesTheDefaultRankAndForgetsTheGivenOne()
    {
        ServerPlayer admin = new ConnectedPlayers().Connect("Admin");
        PlayerRanks.Grant(admin, Perms.ADMIN);

        await new DeopCommand(Options.Create(new SubnauticaServerOptions { DefaultPlayerPerm = Perms.NONE })).Execute(new RecordingCommandContext(), new KnownPlayer(admin));

        admin.Permissions.Should().Be(Perms.NONE);
        admin.GrantedPermissions.Should().BeNull();
    }

    [TestMethod]
    public async Task AnOfflinePlayerCanBeMutedAndStaysMuted()
    {
        ConnectedPlayers players = new();
        ServerPlayer target = players.Connect("Target");
        players.Disconnect(target.SessionId);

        await new MuteCommand(defaults).Execute(new RecordingCommandContext(), new KnownPlayer(target));
        ServerPlayer rejoined = players.Connect("Target");

        rejoined.PlayerContext.IsMuted.Should().BeTrue();
    }

    [TestMethod]
    public async Task AModeratorCannotMuteAnotherModerator()
    {
        ServerPlayer target = new ConnectedPlayers().Connect("Target");
        target.Permissions = Perms.MODERATOR;
        RecordingCommandContext context = new() { Origin = CommandOrigin.PLAYER, OriginId = (SessionId)99, Permissions = Perms.MODERATOR };

        await new MuteCommand(defaults).Execute(context, new KnownPlayer(target));

        target.IsMuted.Should().BeFalse();
    }
}
