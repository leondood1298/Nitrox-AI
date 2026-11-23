using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Models.Packets.Processors;
using Nitrox.Test.Server.Helper;
using NSubstitute;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.Packets.Processors;

/// <summary>
///     The stats of a game still on the loading screen are a fresh player's, not the player's.
/// </summary>
[TestClass]
public sealed class PlayerStatsProcessorTest
{
    [TestMethod]
    public async Task StatsFromALoadingGameAreIgnored()
    {
        ServerPlayer player = new ConnectedPlayers().Connect("Bob");
        PlayerStatsData stored = new(10, 45, 30, 20, 20, 0.5f);
        player.Stats = stored;
        player.HasFinishedInitialSync = false;

        await new PlayerStatsProcessor(NullLogger<PlayerStatsProcessor>.Instance)
            .Process(new AuthProcessorContext(player, Substitute.For<IPacketSender>()), new PlayerStats(player.SessionId, 45, 45, 100, 50, 90, 0));

        player.Stats.Should().BeSameAs(stored);
    }

    [TestMethod]
    public async Task StatsFromAPlayerInTheGameAreStored()
    {
        ServerPlayer player = new ConnectedPlayers().Connect("Bob");
        player.HasFinishedInitialSync = true;

        await new PlayerStatsProcessor(NullLogger<PlayerStatsProcessor>.Instance)
            .Process(new AuthProcessorContext(player, Substitute.For<IPacketSender>()), new PlayerStats(player.SessionId, 40, 45, 80, 50, 90, 0));

        player.Stats.Health.Should().Be(80);
    }
}
