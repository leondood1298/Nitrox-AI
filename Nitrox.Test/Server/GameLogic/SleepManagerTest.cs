using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Test.Server.Helper;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.GameLogic;

/// <summary>
///     The night passes when everyone who has finished joining is in bed; a player still loading does not count.
/// </summary>
[TestClass]
public sealed class SleepManagerTest
{
    [TestMethod]
    public void ALoadingPlayerDoesNotCount()
    {
        ConnectedPlayers players = new();
        ServerPlayer inGame = players.Connect("InGame");
        inGame.HasFinishedInitialSync = true;
        players.Connect("Loading").HasFinishedInitialSync = false;

        int playersInGame = SleepManager.CountPlayersInGame(players.PlayerManager.ConnectedPlayers());

        playersInGame.Should().Be(1);
        SleepManager.AreAllInBed(1, playersInGame).Should().BeTrue("the one player in the game is in bed");
    }

    [TestMethod]
    public void EveryoneInTheGameMustBeInBed()
    {
        ConnectedPlayers players = new();
        players.Connect("Alice").HasFinishedInitialSync = true;
        players.Connect("Bruno").HasFinishedInitialSync = true;

        int playersInGame = SleepManager.CountPlayersInGame(players.PlayerManager.ConnectedPlayers());

        SleepManager.AreAllInBed(1, playersInGame).Should().BeFalse();
        SleepManager.AreAllInBed(2, playersInGame).Should().BeTrue();
    }

    [TestMethod]
    public void TheLeavingPlayerDoesNotCount()
    {
        ConnectedPlayers players = new();
        players.Connect("Stays").HasFinishedInitialSync = true;
        ServerPlayer leaving = players.Connect("Leaves");
        leaving.HasFinishedInitialSync = true;

        SleepManager.CountPlayersInGame(players.PlayerManager.ConnectedPlayers(), leaving.SessionId).Should().Be(1);
    }

    [TestMethod]
    public void NobodyInTheGameIsNoNight()
    {
        SleepManager.AreAllInBed(0, 0).Should().BeFalse();
    }
}
