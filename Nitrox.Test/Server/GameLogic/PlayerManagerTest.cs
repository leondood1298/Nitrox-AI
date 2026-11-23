using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.MultiplayerSession;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Test.Server.Helper;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.GameLogic;

/// <summary>
///     Names: a name is free again whenever its session ends, and belongs to the game it first joined from.
/// </summary>
[TestClass]
public sealed class PlayerManagerTest
{
    [TestMethod]
    public void ANameIsFreedWhenItsSessionEndsBeforeJoining()
    {
        ConnectedPlayers players = new();
        SessionId queued = players.Reserve("Bob", "key", out _);

        players.Disconnect(queued);
        players.Reserve("Bob", "key", out MultiplayerSessionReservation again);

        again.ReservationState.Should().Be(MultiplayerSessionReservationState.RESERVED, "a player who quit in the join queue must be able to come back");
    }

    [TestMethod]
    public void ANameInUseIsRefused()
    {
        ConnectedPlayers players = new();
        players.Connect("Bob");

        players.Reserve("Bob", "key", out MultiplayerSessionReservation second);

        second.ReservationState.HasStateFlag(MultiplayerSessionReservationState.UNIQUE_PLAYER_NAME_CONSTRAINT_VIOLATED).Should().BeTrue();
    }

    [TestMethod]
    public void AConnectedPlayersNameIsFreedOnDisconnect()
    {
        ConnectedPlayers players = new();
        ServerPlayer bob = players.Connect("Bob");

        players.Disconnect(bob.SessionId);

        players.Reserve("Bob", "key", out MultiplayerSessionReservation again);
        again.ReservationState.Should().Be(MultiplayerSessionReservationState.RESERVED);
    }

    [TestMethod]
    public void AnotherKeyCannotTakeOverAName()
    {
        ConnectedPlayers players = new();
        ServerPlayer bob = players.Connect("Bob", "bobs-key");
        players.Disconnect(bob.SessionId);

        players.Reserve("Bob", "someone-else", out MultiplayerSessionReservation impostor);

        impostor.ReservationState.HasStateFlag(MultiplayerSessionReservationState.PLAYER_KEY_MISMATCH).Should().BeTrue();
        players.Reserve("Bob", "bobs-key", out MultiplayerSessionReservation owner);
        owner.ReservationState.Should().Be(MultiplayerSessionReservationState.RESERVED, "the refused attempt must not hold the name");
    }

    [TestMethod]
    public void AGameWithoutAKeyCannotTakeOverAName()
    {
        ConnectedPlayers players = new();
        ServerPlayer bob = players.Connect("Bob", "bobs-key");
        players.Disconnect(bob.SessionId);

        players.Reserve("Bob", "", out MultiplayerSessionReservation keyless);

        keyless.ReservationState.HasStateFlag(MultiplayerSessionReservationState.PLAYER_KEY_MISMATCH).Should().BeTrue();
    }

    [TestMethod]
    public void ForgettingTheKeyLetsTheNextGameKeepTheName()
    {
        ConnectedPlayers players = new();
        ServerPlayer bob = players.Connect("Bob", "old-key");
        players.Disconnect(bob.SessionId);

        players.PlayerManager.ForgetIdentityKey("Bob").Should().BeTrue();
        ServerPlayer rejoined = players.Connect("Bob", "new-key");
        players.Disconnect(rejoined.SessionId);

        players.Reserve("Bob", "old-key", out MultiplayerSessionReservation old);
        old.ReservationState.HasStateFlag(MultiplayerSessionReservationState.PLAYER_KEY_MISMATCH).Should().BeTrue();
        players.PlayerManager.ForgetIdentityKey("Nobody").Should().BeFalse();
    }

    [TestMethod]
    public void APlayerSavedBeforeKeysKeepsTheFirstKeyTheyJoinWith()
    {
        ConnectedPlayers players = new();
        ServerPlayer bob = players.Connect("Bob", "first");
        bob.IdentityKeyHash = null;
        players.Disconnect(bob.SessionId);

        ServerPlayer rejoined = players.Connect("Bob", "first");

        rejoined.IdentityKeyHash.Should().NotBeNull();
    }

    [TestMethod]
    public void AQueuedPlayerIsFoundByName()
    {
        ConnectedPlayers players = new();
        SessionId queued = players.Reserve("Bob", "key", out _);

        players.PlayerManager.TryGetSessionByName("Bob", out SessionId found).Should().BeTrue();
        found.Should().Be(queued);
        players.PlayerManager.IsReserved(queued).Should().BeTrue();
        players.PlayerManager.TryGetSessionByName("Player", out _).Should().BeFalse("the reserved word is no session");
    }

    [TestMethod]
    public void OutOfCellEntitiesOfAnEarlierSessionAreForgotten()
    {
        ConnectedPlayers players = new();
        ServerPlayer bob = players.Connect("Bob");
        bob.OutOfCellVisibleEntities.Add(new NitroxId());
        players.Disconnect(bob.SessionId);

        ServerPlayer rejoined = players.Connect("Bob");

        rejoined.OutOfCellVisibleEntities.Should().BeEmpty();
    }
}
