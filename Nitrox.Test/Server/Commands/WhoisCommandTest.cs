using System.Threading.Tasks;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Commands;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Test.Server.Helper;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.Commands;

/// <summary>
///     whois told any player where any other player was.
/// </summary>
[TestClass]
public sealed class WhoisCommandTest
{
    [TestMethod]
    public async Task APlayerDoesNotLearnAnotherPlayersPosition()
    {
        ServerPlayer bob = new ConnectedPlayers().Connect("Bob");
        RecordingCommandContext player = new() { Origin = CommandOrigin.PLAYER, OriginId = (SessionId)99, Permissions = Perms.PLAYER };

        await new WhoisCommand().Execute(player, bob);

        player.Replies.Single().Should().NotContain("Position");
    }

    [TestMethod]
    public async Task AModeratorDoes()
    {
        ServerPlayer bob = new ConnectedPlayers().Connect("Bob");
        RecordingCommandContext moderator = new() { Origin = CommandOrigin.PLAYER, OriginId = (SessionId)99, Permissions = Perms.MODERATOR };

        await new WhoisCommand().Execute(moderator, bob);

        moderator.Replies.Single().Should().Contain("Position");
    }
}
