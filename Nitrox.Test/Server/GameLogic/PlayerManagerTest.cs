using System.IO;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Configuration;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Administration;
using NitroxClient.GameLogic.PlayerLogic.PlayerPreferences;
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
    public void AGameWithoutAKeyIsRefused()
    {
        ConnectedPlayers players = new();
        ServerPlayer bob = players.Connect("Bob", "bobs-key");
        players.Disconnect(bob.SessionId);

        players.Reserve("Bob", "", out MultiplayerSessionReservation keyless);
        players.Reserve("Alice", "", out MultiplayerSessionReservation newKeyless);

        keyless.ReservationState.HasStateFlag(MultiplayerSessionReservationState.AUTHENTICATION_FAILED).Should().BeTrue();
        newKeyless.ReservationState.HasStateFlag(MultiplayerSessionReservationState.AUTHENTICATION_FAILED).Should().BeTrue();
    }

    [TestMethod]
    public void ASignatureSeenOnceCannotBeUsedAgain()
    {
        // Someone who saw Bob's public key and signature on the network sends them again on their own connection: their
        // connection got another challenge, so the signature does not match it
        ConnectedPlayers players = new();
        string publicKey = "";
        string signature = "";
        players.Reserve(challenge =>
        {
            PlayerKeys.Proof proof = players.Prove("bobs-key", challenge);
            publicKey = proof.PublicKey;
            signature = proof.Signature;
            return new AuthenticationContext("Bob", Optional.Empty, proof.PublicKey, proof.Signature);
        }, out MultiplayerSessionReservation bobs);
        bobs.ReservationState.Should().Be(MultiplayerSessionReservationState.RESERVED);

        players.Reserve(_ => new AuthenticationContext("Mallory", Optional.Empty, publicKey, signature), out MultiplayerSessionReservation replayed);

        replayed.ReservationState.HasStateFlag(MultiplayerSessionReservationState.AUTHENTICATION_FAILED).Should().BeTrue();
    }

    [TestMethod]
    public void ASignatureForAnotherWorldIsRefused()
    {
        // A server posing as this world got Bob's game to sign its own challenge for this world's identity: the real server
        // never issued that challenge
        ConnectedPlayers players = new();
        PlayerKeys.Proof phished = players.Prove("bobs-key", "the-rogue-servers-challenge");

        players.Reserve(_ => new AuthenticationContext("Bob", Optional.Empty, phished.PublicKey, phished.Signature), out MultiplayerSessionReservation reservation);

        reservation.ReservationState.HasStateFlag(MultiplayerSessionReservationState.AUTHENTICATION_FAILED).Should().BeTrue();
    }

    [TestMethod]
    public void TheNameServerIsRefusedInAnyCase()
    {
        ConnectedPlayers players = new();

        players.Reserve("Server", "key", out MultiplayerSessionReservation upper);
        players.Reserve("sErVeR", "key", out MultiplayerSessionReservation mixed);

        upper.ReservationState.HasStateFlag(MultiplayerSessionReservationState.INCORRECT_USERNAME).Should().BeTrue();
        mixed.ReservationState.HasStateFlag(MultiplayerSessionReservationState.INCORRECT_USERNAME).Should().BeTrue();
    }

    [TestMethod]
    public void WrongServerPasswordsAreSlowedDown()
    {
        ConnectedPlayers players = new(options: new SubnauticaServerOptions { ServerPassword = "secret" });
        IPAddress address = IPAddress.Parse("203.0.113.7");
        for (int i = 0; i < GuessLimiter.FREE_FAILURES; i++)
        {
            players.Reserve($"Bob{i:000}", "key", out _, address, Optional.Of("wrong"));
        }

        players.Reserve("Bob", "key", out MultiplayerSessionReservation right, address, Optional.Of("secret"));

        right.ReservationState.HasStateFlag(MultiplayerSessionReservationState.AUTHENTICATION_FAILED).Should().BeTrue("the address must wait before it may try again");
        right.RejectionReason.Should().Contain("Try again");
    }

    [TestMethod]
    public void ABannedKeyIsRefusedFromAnyAddress()
    {
        ConnectedPlayers players = new();
        players.BanService.BanAsync(null, ConnectedPlayers.PublicKeyOf("bobs-key"), "Bob", "", "TEST", Perms.ADMIN, TimeSpan.Zero).GetAwaiter().GetResult();

        players.Reserve("Bobby", "bobs-key", out MultiplayerSessionReservation reservation, IPAddress.Parse("198.51.100.4"));

        reservation.ReservationState.HasStateFlag(MultiplayerSessionReservationState.PLAYER_BANNED).Should().BeTrue();
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
        bob.IdentityPublicKey = null;
        players.Disconnect(bob.SessionId);

        ServerPlayer rejoined = players.Connect("Bob", "first");

        rejoined.IdentityPublicKey.Should().Be(ConnectedPlayers.PublicKeyOf("first"));
    }

    [TestMethod]
    public void ARankIsKeptOnlyWhenTheSameKeyIsProvenAgain()
    {
        ConnectedPlayers players = new();
        ServerPlayer bob = players.Connect("Bob", "bobs-key");
        bob.IdentityVerifiedThisSession.Should().BeFalse("the name was tied to the key just now");
        PlayerRanks.Grant(bob, Perms.ADMIN);
        players.Disconnect(bob.SessionId);

        ServerPlayer rejoined = players.Connect("Bob", "bobs-key");

        rejoined.IdentityVerifiedThisSession.Should().BeTrue();
        PlayerRanks.OnJoin(rejoined, Perms.PLAYER, false, null).Should().Be(Perms.ADMIN);
    }

    [TestMethod]
    public void ForgettingTheKeyDropsTheRank()
    {
        ConnectedPlayers players = new();
        ServerPlayer bob = players.Connect("Bob", "bobs-key");
        PlayerRanks.Grant(bob, Perms.ADMIN);
        players.Disconnect(bob.SessionId);
        players.PlayerManager.ForgetIdentityKey("Bob");

        ServerPlayer taker = players.Connect("Bob", "someone-else");

        taker.IdentityVerifiedThisSession.Should().BeFalse();
        taker.GrantedPermissions.Should().BeNull();
        PlayerRanks.OnJoin(taker, Perms.PLAYER, false, null).Should().Be(Perms.PLAYER);
    }

    [TestMethod]
    public void AMuteOutlastsLeaving()
    {
        ConnectedPlayers players = new();
        ServerPlayer bob = players.Connect("Bob");
        PlayerMutes.Set(bob, true);
        players.Disconnect(bob.SessionId);

        ServerPlayer rejoined = players.Connect("Bob");

        rejoined.PlayerContext.IsMuted.Should().BeTrue();
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
                IdentityPublicKey = ConnectedPlayers.PublicKeyOf("made-for-the-old-identity"),
                GrantedPermissions = Perms.ADMIN,
                NitroxId = new NitroxId(),
                PlayerPreferences = new SubnauticaPlayerPreferences(new Dictionary<string, PingInstancePreference>(), [])
            }.ToPlayer();
            players.PlayerManager.AddSavedPlayer(saved);

            players.Reserve("Bob", "world-derived", out MultiplayerSessionReservation first);
            first.ReservationState.Should().Be(MultiplayerSessionReservationState.RESERVED);
            saved.GrantedPermissions.Should().BeNull("whoever takes the name now must not inherit its rank");
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
