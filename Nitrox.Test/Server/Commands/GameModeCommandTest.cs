using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Commands;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Test.Server.Helper;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.Commands;

/// <summary>
///     A changed game mode did not reach the player's connection context, which is what players joining later are sent, and
///     a number that names no game mode was accepted.
/// </summary>
[TestClass]
public sealed class GameModeCommandTest
{
    [TestMethod]
    public async Task PlayersJoiningLaterSeeTheNewGameMode()
    {
        ServerPlayer bob = new ConnectedPlayers().Connect("Bob");

        await new GameModeCommand().Execute(new RecordingCommandContext(), SubnauticaGameMode.CREATIVE, bob);

        bob.GameMode.Should().Be(SubnauticaGameMode.CREATIVE);
        bob.PlayerContext.GameMode.Should().Be(SubnauticaGameMode.CREATIVE);
    }

    [TestMethod]
    public void ANumberThatNamesNoGameModeIsRefused()
    {
        CommandRegistryService registry = new([], [], NullLogger<CommandRegistryService>.Instance);

        registry.TryParseToType("5", typeof(SubnauticaGameMode)).Should().BeNull();
        registry.TryParseToType("2", typeof(SubnauticaGameMode)).Should().Be(SubnauticaGameMode.FREEDOM);
        registry.TryParseToType("creative", typeof(SubnauticaGameMode)).Should().Be(SubnauticaGameMode.CREATIVE);
    }
}
