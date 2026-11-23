using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
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
    ///     The hash of the key a reserving session sent, until it becomes a connected player and the key is stored with
    ///     the player.
    /// </summary>
    private readonly ThreadSafeDictionary<SessionId, string> pendingKeyHashes = [];

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

        if (!string.IsNullOrEmpty(options.Value.ServerPassword) && (!authenticationContext.ServerPassword.HasValue || authenticationContext.ServerPassword.Value != options.Value.ServerPassword))
        {
            MultiplayerSessionReservationState rejectedState = MultiplayerSessionReservationState.REJECTED | MultiplayerSessionReservationState.AUTHENTICATION_FAILED;
            return new MultiplayerSessionReservation(sessionId, rejectedState);
        }


        if (!PlayerNameRegex().IsMatch(authenticationContext.Username))
        {
            MultiplayerSessionReservationState rejectedState = MultiplayerSessionReservationState.REJECTED | MultiplayerSessionReservationState.INCORRECT_USERNAME;
            return new MultiplayerSessionReservation(sessionId, rejectedState);
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
        string keyHash = PlayerIdentityKey.Hash(authenticationContext.PlayerKey);
        if (player != null && !PlayerIdentityKey.Matches(player.IdentityKeyHash, keyHash))
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
        if (keyHash != null)
        {
            pendingKeyHashes[sessionId] = keyHash;
        }

        bool hasSeenPlayerBefore = player != null;
        NitroxId playerNitroxId = hasSeenPlayerBefore ? player.GameObjectId : new NitroxId();
        SubnauticaGameMode gameMode = hasSeenPlayerBefore ? player.GameMode : options.Value.GameMode;
        IntroCinematicMode introCinematicMode = hasSeenPlayerBefore ? IntroCinematicMode.COMPLETED : IntroCinematicMode.LOADING;
        PlayerAnimation animation = new(AnimChangeType.UNDERWATER, AnimChangeState.ON);

        SessionManager.Session session = sessionManager.GetOrCreateSession(endPoint);

        // TODO: At some point, store the muted state of a player
        PlayerContext playerContext = new(playerName, session.Id, playerNitroxId, !hasSeenPlayerBefore, playerSettings, false, gameMode, null, introCinematicMode, animation);

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
        if (pendingKeyHashes.Remove(sessionId, out string keyHash) && player.IdentityKeyHash == null)
        {
            player.IdentityKeyHash = keyHash;
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
        player.IdentityKeyHash = null;
        return true;
    }

    public Task OnEventAsync(ISessionCleaner.Args args)
    {
        reservations.Remove(args.Session.Id);
        pendingKeyHashes.Remove(args.Session.Id);
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
            if (player.IdentityKeyHash != null)
            {
                player.IdentityKeyHash = null;
                untied++;
            }
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
