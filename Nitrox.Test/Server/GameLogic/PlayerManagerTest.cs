using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Core;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Players;
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
    public void AWorldThatGetsItsIdentityNowUntiesNamesOnce()
    {
        // A world from before identities existed (or from a build that derived keys from the address) has names tied to
        // keys no game will send again; with a new identity each name keeps the next key it joins with
        string saveDirectory = Path.Combine(Path.GetTempPath(), $"nitrox-untie-{Guid.NewGuid():N}");
        try
        {
            ConnectedPlayers players = new(new WorldIdentity(() => saveDirectory, NullLogger<WorldIdentity>.Instance));
            Nitrox.Server.Subnautica.Models.Player saved = new PersistedPlayerData
            {
                Name = "Bob",
                IdentityKeyHash = PlayerIdentityKey.Hash("address-derived"),
                NitroxId = new NitroxId(),
                PlayerPreferences = new SubnauticaPlayerPreferences(new Dictionary<string, PingInstancePreference>(), [])
            }.ToPlayer();
            players.PlayerManager.AddSavedPlayer(saved);

            players.Reserve("Bob", "world-derived", out MultiplayerSessionReservation first);
            first.ReservationState.Should().Be(MultiplayerSessionReservationState.RESERVED);
        }
        finally
        {
            if (Directory.Exists(saveDirectory))
            {
                Directory.Delete(saveDirectory, true);
            }
        }
    }

    [TestMethod]
    public void AWorldWithAKeptIdentityKeepsNamesTied()
    {
        string saveDirectory = Path.Combine(Path.GetTempPath(), $"nitrox-untie-{Guid.NewGuid():N}");
        try
        {
            _ = new WorldIdentity(() => saveDirectory, NullLogger<WorldIdentity>.Instance).Value;
            ConnectedPlayers players = new(new WorldIdentity(() => saveDirectory, NullLogger<WorldIdentity>.Instance));
            ServerPlayer bob = players.Connect("Bob", "bobs-key");
            players.Disconnect(bob.SessionId);

            players.Reserve("Bob", "someone-else", out MultiplayerSessionReservation impostor);

            impostor.ReservationState.HasStateFlag(MultiplayerSessionReservationState.PLAYER_KEY_MISMATCH).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(saveDirectory))
            {
                Directory.Delete(saveDirectory, true);
            }
        }
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
