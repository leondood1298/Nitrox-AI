using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Nitrox.Model.Constants;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.GameLogic.PlayerAnimation;
using Nitrox.Model.MultiplayerSession;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.MultiplayerSession;
using Nitrox.Server.Subnautica.Models.Administration;
using Nitrox.Server.Subnautica.Models.AppEvents;
using Nitrox.Server.Subnautica.Models.Communication;
using Nitrox.Server.Subnautica.Services;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

// TODO: This manager should only handle player data. Move connection related state to other managers.
internal sealed partial class PlayerManager(SessionManager sessionManager, BanService banService, IOptions<SubnauticaServerOptions> options, ILogger<PlayerManager> logger, WorldIdentity? worldIdentity = null)
    : ISessionCleaner
{
    private readonly WorldIdentity? worldIdentity = worldIdentity;
    private int namesUntiedForNewIdentity;

    [GeneratedRegex(NitroxConstants.PLAYER_NAME_VALID_REGEX, RegexOptions.NonBacktracking)]
    private static partial Regex PlayerNameRegex();

    private readonly ThreadSafeDictionary<string, Player> allPlayersByName = [];
    private readonly ThreadSafeDictionary<SessionId, Player> connectedPlayersBySessionId = [];
    private readonly ThreadSafeDictionary<SessionId, PlayerContext> reservations = [];

    /// <summary>
    ///     The names in use, with the session holding each, from the reservation until that session ends. Keyed by
    ///     session so that a session which ends before it became a connected player (in the queue, or while loading the
    ///     scene) frees its name again (taken from upstream pull request 2894).
    /// </summary>
    private readonly ThreadSafeDictionary<string, SessionId> reservedPlayerNames = new()
    {
        // "Player" is often used to identify the local player and should not be used by any user
        { "Player", SessionId.SERVER_ID }
    };

    /// <summary>
    ///     The public key a reserving session proved it holds, and whether the name was already tied to it, until the
    ///     session becomes a connected player and the key is stored with the player.
    /// </summary>
    private readonly ThreadSafeDictionary<SessionId, PendingKey> pendingKeys = [];

    /// <summary>
    ///     The challenge each connecting session was sent in its session policy, used once by its reservation.
    /// </summary>
    private readonly ThreadSafeDictionary<SessionId, string> joinChallenges = [];

    /// <summary>
    ///     Names nobody may join with: "Server" is how server messages are shown in chat.
    /// </summary>
    private static readonly string[] RESERVED_NAMES = ["Server"];

    private readonly GuessLimiter passwordGuesses = new();

    private sealed record PendingKey(string PublicKey, bool WasTiedBefore);

    private readonly SessionManager sessionManager = sessionManager;
    private readonly BanService banService = banService;
    private readonly IOptions<SubnauticaServerOptions> options = options;
    private readonly ILogger<PlayerManager> logger = logger;
    private PeerId currentPlayerId;

    /// <summary>All players that have joined since the server started, even if they disconnected</summary>
    public IEnumerable<Player> GetAllPlayers() => allPlayersByName.Values;

    public IEnumerable<Player> ConnectedPlayers()
    {
        return connectedPlayersBySessionId.Values;
    }

    public List<Player> GetConnectedPlayers() => ConnectedPlayers().ToList();

    public List<Player> GetConnectedPlayersExcept(Player excludePlayer)
    {
        return ConnectedPlayers().Where(player => player != excludePlayer).ToList();
    }

    public List<Player> GetConnectedPlayersExcept(SessionId excludeSessionId)
    {
        return ConnectedPlayers().Where(player => player.SessionId != excludeSessionId).ToList();
    }

    public PlayerContext? GetPlayerReservation(SessionId sessionId)
    {
        return reservations.TryGetValue(sessionId, out PlayerContext player) ? player : null;
    }

    public void AddSavedPlayer(Player player)
    {
        allPlayersByName.Add(player.Name, player);
        currentPlayerId = allPlayersByName.Values.Max(x => x.PeerId);
    }

    public MultiplayerSessionReservation ReservePlayerContext(
        SessionId sessionId,
        IPEndPoint endPoint,
        PlayerSettings playerSettings,
        AuthenticationContext authenticationContext)
    {
        if (banService.IsBanned(endPoint.Address))
        {
            MultiplayerSessionReservationState rejectedState = MultiplayerSessionReservationState.REJECTED | MultiplayerSessionReservationState.PLAYER_BANNED;
            return new MultiplayerSessionReservation(sessionId, rejectedState, banService.GetBanRejectionDetail(endPoint.Address));
        }

        if (sessionManager.GetSessionCount() > options.Value.MaxConnections)
        {
            MultiplayerSessionReservationState rejectedState = MultiplayerSessionReservationState.REJECTED | MultiplayerSessionReservationState.SERVER_PLAYER_CAPACITY_REACHED;
            return new MultiplayerSessionReservation(sessionId, rejectedState);
        }

        if (!string.IsNullOrEmpty(options.Value.ServerPassword))
        {
            string[] guessKeys = [$"address:{endPoint.Address}", $"session:{sessionId}"];
            TimeSpan wait = passwordGuesses.GetRemainingWait(guessKeys);
            if (wait > TimeSpan.Zero)
            {
                MultiplayerSessionReservationState rejectedState = MultiplayerSessionReservationState.REJECTED | MultiplayerSessionReservationState.AUTHENTICATION_FAILED;
                return new MultiplayerSessionReservation(sessionId, rejectedState, $"Too many wrong passwords. Try again in {Math.Ceiling(wait.TotalSeconds)} seconds.");
            }
            if (!authenticationContext.ServerPassword.HasValue || !SecretComparison.AreEqual(authenticationContext.ServerPassword.Value, options.Value.ServerPassword))
            {
                passwordGuesses.RecordFailure(guessKeys);
                MultiplayerSessionReservationState rejectedState = MultiplayerSessionReservationState.REJECTED | MultiplayerSessionReservationState.AUTHENTICATION_FAILED;
                return new MultiplayerSessionReservation(sessionId, rejectedState);
            }
            passwordGuesses.RecordSuccess(guessKeys);
        }

        if (!IsAllowedName(authenticationContext.Username))
        {
            MultiplayerSessionReservationState rejectedState = MultiplayerSessionReservationState.REJECTED | MultiplayerSessionReservationState.INCORRECT_USERNAME;
            return new MultiplayerSessionReservation(sessionId, rejectedState);
        }

        // The key is proven by signing this connection's challenge, so a key seen once (on the network, or by a server
        // posing as this world) cannot be used again.
        joinChallenges.Remove(sessionId, out string? challenge);
        string? publicKey = authenticationContext.PlayerPublicKey;
        if (!PlayerIdentityProof.Verify(publicKey, authenticationContext.PlayerSignature, worldIdentity?.Value ?? "", challenge))
        {
            logger.ZLogWarning($"Refused {authenticationContext.Username} from {endPoint}: the game did not prove it holds a player key");
            MultiplayerSessionReservationState rejectedState = MultiplayerSessionReservationState.REJECTED | MultiplayerSessionReservationState.AUTHENTICATION_FAILED;
            return new MultiplayerSessionReservation(sessionId, rejectedState);
        }

        if (banService.IsKeyBanned(publicKey))
        {
            MultiplayerSessionReservationState rejectedState = MultiplayerSessionReservationState.REJECTED | MultiplayerSessionReservationState.PLAYER_BANNED;
            return new MultiplayerSessionReservation(sessionId, rejectedState, banService.GetKeyBanRejectionDetail(publicKey));
        }

        string playerName = authenticationContext.Username;

        allPlayersByName.TryGetValue(playerName, out Player? player);
        // The player's own game mode, as for the death itself (PlayerDeaths)
        if (player?.IsPermaDeath == true && PlayerDeaths.IsPermanent(player))
        {
            MultiplayerSessionReservationState rejectedState = MultiplayerSessionReservationState.REJECTED | MultiplayerSessionReservationState.HARDCORE_PLAYER_DEAD;
            return new MultiplayerSessionReservation(sessionId, rejectedState);
        }

        UntieNamesIfTheWorldIdentityIsNew();
        if (player != null && !PlayerIdentityKey.Matches(player.IdentityPublicKey, publicKey))
        {
            logger.ZLogWarning($"Refused {playerName} from {endPoint}: the key does not match the one this name joined with before");
            MultiplayerSessionReservationState rejectedState = MultiplayerSessionReservationState.REJECTED | MultiplayerSessionReservationState.PLAYER_KEY_MISMATCH;
            return new MultiplayerSessionReservation(sessionId, rejectedState);
        }

        if (!reservedPlayerNames.TryAdd(playerName, sessionId))
        {
            MultiplayerSessionReservationState rejectedState = MultiplayerSessionReservationState.REJECTED | MultiplayerSessionReservationState.UNIQUE_PLAYER_NAME_CONSTRAINT_VIOLATED;
            return new MultiplayerSessionReservation(sessionId, rejectedState);
        }
        pendingKeys[sessionId] = new PendingKey(publicKey, player?.IdentityPublicKey != null);

        bool hasSeenPlayerBefore = player != null;
        NitroxId playerNitroxId = hasSeenPlayerBefore ? player.GameObjectId : new NitroxId();
        SubnauticaGameMode gameMode = hasSeenPlayerBefore ? player.GameMode : options.Value.GameMode;
        IntroCinematicMode introCinematicMode = hasSeenPlayerBefore ? IntroCinematicMode.COMPLETED : IntroCinematicMode.LOADING;
        PlayerAnimation animation = new(AnimChangeType.UNDERWATER, AnimChangeState.ON);

        SessionManager.Session session = sessionManager.GetOrCreateSession(endPoint);

        PlayerContext playerContext = new(playerName, session.Id, playerNitroxId, !hasSeenPlayerBefore, playerSettings, player?.IsMuted ?? false, gameMode, null, introCinematicMode, animation);

        reservations.Add(session.Id, playerContext);

        return new MultiplayerSessionReservation(sessionId);
    }

    public Player CreatePlayerData(SessionId sessionId, out bool wasBrandNewPlayer)
    {
        PlayerContext playerContext = reservations[sessionId];
        Validate.NotNull(playerContext);

        wasBrandNewPlayer = playerContext.WasBrandNewPlayer;

        if (!allPlayersByName.TryGetValue(playerContext.PlayerName, out Player player))
        {
            player = new Player(++currentPlayerId,
                                sessionId,
                                playerContext.PlayerName,
                                false,
                                playerContext,
                                NitroxVector3.Zero,
                                NitroxQuaternion.Identity,
                                playerContext.PlayerNitroxId,
                                Optional.Empty,
                                options.Value.DefaultPlayerPerm,
                                new(options.Value.DefaultOxygenValue, options.Value.DefaultMaxOxygenValue, options.Value.DefaultHealthValue, options.Value.DefaultHungerValue, options.Value.DefaultThirstValue, options.Value.DefaultInfectionValue),
                                options.Value.GameMode,
                                [],
                                [],
                                new Dictionary<string, NitroxId>(),
                                new Dictionary<string, float>(),
                                new Dictionary<string, PingInstancePreference>(),
                                [],
                                false,
                                true
            );
            allPlayersByName[playerContext.PlayerName] = player;
        }
        else
        {
            player.SessionId = sessionId;
        }

        // The first key a name joins with is the one it keeps (also for a player saved before keys existed)
        player.IdentityVerifiedThisSession = false;
        if (pendingKeys.Remove(sessionId, out PendingKey? pendingKey))
        {
            if (player.IdentityPublicKey == null)
            {
                player.IdentityPublicKey = pendingKey.PublicKey;
            }
            // Untying the name while this session waited in the queue makes the name new again
            player.IdentityVerifiedThisSession = pendingKey.WasTiedBefore && player.IdentityPublicKey == pendingKey.PublicKey;
        }

        player.HasFinishedInitialSync = false;
        connectedPlayersBySessionId.Add(playerContext.SessionId, player);

        // TODO: make a ConnectedPlayer wrapper so this is not stateful
        player.PlayerContext = playerContext;

        // reconnecting players need to have their cell visibility refreshed, and nothing out of their cells is known to
        // the new session yet
        player.ClearVisibleCells();
        player.OutOfCellVisibleEntities.Clear();

        reservations.Remove(sessionId);

        return player;
    }

    public bool SetPlayerProperty<T>(SessionId sessionId, T value, Action<Player, T> action)
    {
        if (!TryGetPlayerBySessionId(sessionId, out Player? player))
        {
            return false;
        }

        action(player, value);
        return true;
    }

    public bool TryGetPlayerByName(string playerName, [NotNullWhen(true)] out Player? foundPlayer)
    {
        foundPlayer = null;
        foreach (Player player in ConnectedPlayers())
        {
            if (player.Name == playerName)
            {
                foundPlayer = player;
                return true;
            }
        }

        return false;
    }

    public bool TryGetPlayerBySessionId(SessionId sessionId, [NotNullWhen(true)] out Player? player)
    {
        return connectedPlayersBySessionId.TryGetValue(sessionId, out player);
    }

    /// <summary>
    ///     The session holding a name, also while that player waits in the join queue or loads, before it is a connected
    ///     player.
    /// </summary>
    public bool TryGetSessionByName(string playerName, out SessionId sessionId)
    {
        return reservedPlayerNames.TryGetValue(playerName, out sessionId) && sessionId != SessionId.SERVER_ID;
    }

    /// <summary>
    ///     True while the session has a reservation and is not yet a connected player (queued or loading the scene).
    /// </summary>
    public bool IsReserved(SessionId sessionId) => reservations.ContainsKey(sessionId);

    /// <summary>
    ///     The sessions that hold a reservation and are not players yet.
    /// </summary>
    public List<SessionId> GetReservedSessions() => [.. reservations.Keys];

    /// <summary>
    ///     Forgets the key a name is tied to, so that the next player to join with that name ties it to theirs.
    /// </summary>
    /// <returns>False if no player with that name has joined this server.</returns>
    public bool ForgetIdentityKey(string playerName)
    {
        if (!allPlayersByName.TryGetValue(playerName, out Player player))
        {
            return false;
        }
        PlayerRanks.Untie(player);
        return true;
    }

    /// <summary>
    ///     Any player who has joined this world under <paramref name="playerName" />, online or not.
    /// </summary>
    public bool TryGetKnownPlayerByName(string playerName, [NotNullWhen(true)] out Player? player)
    {
        return allPlayersByName.TryGetValue(playerName, out player);
    }

    /// <summary>
    ///     The key a queued or loading session proved it holds, before it is stored with the player.
    /// </summary>
    public string? GetPendingPublicKey(SessionId sessionId) => pendingKeys.TryGetValue(sessionId, out PendingKey? pending) ? pending.PublicKey : null;

    /// <summary>
    ///     The rank a queued session will have once it joins, so that a command cannot act on someone it could not act on
    ///     a moment later.
    /// </summary>
    public Perms GetRankOnJoin(SessionId sessionId)
    {
        Perms defaultPerms = options.Value.DefaultPlayerPerm;
        Player? player = null;
        if (reservations.TryGetValue(sessionId, out PlayerContext? context))
        {
            allPlayersByName.TryGetValue(context.PlayerName, out player);
        }
        bool verified = pendingKeys.TryGetValue(sessionId, out PendingKey? pending) && pending.WasTiedBefore && player?.IdentityPublicKey == pending.PublicKey;
        return PlayerRanks.OnJoin(verified, player?.GrantedPermissions, defaultPerms, options.Value.LocalhostIsAdmin, sessionManager.GetEndPoint(sessionId)?.Address);
    }

    /// <summary>
    ///     Makes the challenge the connecting session must sign to join (see <see cref="PlayerIdentityProof" />). A new one
    ///     replaces any earlier one of the same session.
    /// </summary>
    public string IssueJoinChallenge(SessionId sessionId)
    {
        string challenge = Convert.ToBase64String(RandomNumberGenerator.GetBytes(PlayerIdentityProof.CHALLENGE_LENGTH));
        joinChallenges[sessionId] = challenge;
        return challenge;
    }

    /// <summary>
    ///     Names a player may join with: the allowed characters, and none that passes for the server in chat.
    /// </summary>
    internal static bool IsAllowedName(string? name)
    {
        if (name == null || !PlayerNameRegex().IsMatch(name))
        {
            return false;
        }
        return !RESERVED_NAMES.Contains(name.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public Task OnEventAsync(ISessionCleaner.Args args)
    {
        reservations.Remove(args.Session.Id);
        pendingKeys.Remove(args.Session.Id);
        joinChallenges.Remove(args.Session.Id);
        ReleaseNameOf(args.Session.Id);
        if (!connectedPlayersBySessionId.Remove(args.Session.Id, out Player player))
        {
            return Task.CompletedTask;
        }
        player.IsOnline = false;
        logger.ZLogInformation($"{player.Name} left the game");
        return Task.CompletedTask;
    }

    /// <summary>
    ///     The keys names are tied to are derived from the world identity. When this run had to make the identity (a world
    ///     from before identities existed, or a lost identity file), no stored key can match any more, so every name keeps
    ///     the first key it joins with again, once.
    /// </summary>
    private void UntieNamesIfTheWorldIdentityIsNew()
    {
        if (worldIdentity == null || !worldIdentity.CreatedThisRun || Interlocked.Exchange(ref namesUntiedForNewIdentity, 1) == 1)
        {
            return;
        }
        int untied = 0;
        foreach (Player player in allPlayersByName.Values)
        {
            if (player.IdentityPublicKey != null)
            {
                untied++;
            }
            PlayerRanks.Untie(player);
        }
        if (untied > 0)
        {
            logger.ZLogWarning($"The world identity is new: {untied} player name(s) will be tied again to the key they next join with");
        }
    }

    private void ReleaseNameOf(SessionId sessionId)
    {
        string? name = null;
        foreach (KeyValuePair<string, SessionId> pair in reservedPlayerNames)
        {
            if (pair.Value == sessionId)
            {
                name = pair.Key;
                break;
            }
        }
        if (name != null)
        {
            reservedPlayerNames.Remove(name);
        }
    }
}
