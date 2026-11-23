using System.Collections.Generic;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.MultiplayerSession;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models
{
    internal sealed class Player
    {
        private const ushort OFFLINE_SESSION_ID = 0;

        private readonly ThreadSafeSet<AbsoluteEntityCell> visibleCells;

        private ushort sessionId;

        public ThreadSafeList<NitroxTechType> UsedItems { get; }
        public Optional<NitroxId>[] QuickSlotsBindingIds { get; set; }

        public PlayerContext? PlayerContext { get; set; }
        /// <inheritdoc cref="Nitrox.Model.Core.PeerId" />
        public PeerId PeerId { get; init; }

        /// <inheritdoc cref="Nitrox.Model.Core.SessionId" />
        public SessionId SessionId
        {
            get => Interlocked.CompareExchange(ref sessionId, OFFLINE_SESSION_ID, OFFLINE_SESSION_ID);
            set => Interlocked.Exchange(ref sessionId, value);
        }

        public bool IsOnline
        {
            get => SessionId != OFFLINE_SESSION_ID;
            set
            {
                if (value)
                {
                    throw new InvalidOperationException($"{nameof(Player)} cannot be put in an online state without a {nameof(SessionId)}!");
                }
                SessionId = OFFLINE_SESSION_ID;
            }
        }

        public string Name { get; }

        /// <summary>
        ///     The hash of the key this name first joined with, or null if no key is tied to the name yet. A player who
        ///     joins with this name and another key is refused.
        /// </summary>
        public string? IdentityKeyHash { get; set; }

        public bool IsPermaDeath { get; set; }
        public NitroxVector3 Position { get; set; }
        public NitroxQuaternion Rotation { get; set; }
        public NitroxId GameObjectId { get; set; }
        public Optional<NitroxId> SubRootId { get; set; }
        public Perms Permissions { get; set; }
        public PlayerStatsData Stats { get; set; }
        public SubnauticaGameMode GameMode { get; set; }
        public NitroxVector3? LastStoredPosition { get; set; }
        public Optional<NitroxId> LastStoredSubRootID { get; set; }
        public ThreadSafeDictionary<string, float> PersonalCompletedGoalsWithTimestamp { get; }
        public ThreadSafeDictionary<string, PingInstancePreference> PingInstancePreferences { get; set; }
        public ThreadSafeList<int> PinnedRecipePreferences { get; set; }
        public ThreadSafeDictionary<string, NitroxId> EquippedItems { get; set; }
        public ThreadSafeSet<NitroxId> OutOfCellVisibleEntities { get; set; } = [];
        /// <summary>
        ///     The PDA's unread markers of this player. A brand-new player has none, like a new game.
        /// </summary>
        public ThreadSafeSet<PdaNotification> UnreadPdaNotifications { get; set; } = [];
        /// <summary>
        ///     False while the player is still loading the world. Entities handed to such a player are not simulated
        ///     until it is done, so players who are already in the world are preferred.
        /// </summary>
        public bool HasFinishedInitialSync { get; set; }

        /// <summary>
        ///     Where this player died and has not been back since, each shown to them alone as a beacon. Kept so that the
        ///     beacons are still there after the player rejoins.
        /// </summary>
        public ThreadSafeList<NitroxVector3> DeathMarkers { get; } = new();
        public bool InPrecursor { get; set; }
        public bool DisplaySurfaceWater { get; set; }

        public PlayerEntity Entity { get; set; }

        public Player(PeerId peerId, SessionId sessionId, string name, bool isPermaDeath, PlayerContext? playerContext,
                      NitroxVector3 position, NitroxQuaternion rotation, NitroxId playerId, Optional<NitroxId> subRootId, Perms perms, PlayerStatsData stats, SubnauticaGameMode gameMode,
                      IEnumerable<NitroxTechType> usedItems, Optional<NitroxId>[] quickSlotsBindingIds,
                      IDictionary<string, NitroxId> equippedItems, IDictionary<string, float> personalCompletedGoalsWithTimestamp, IDictionary<string, PingInstancePreference> pingInstancePreferences, IList<int> pinnedRecipePreferences, bool inPrecursor,
                      bool displaySurfaceWater)
        {
            PeerId = peerId;
            SessionId = sessionId;
            Name = name;
            IsPermaDeath = isPermaDeath;
            PlayerContext = playerContext;
            Position = position;
            Rotation = rotation;
            SubRootId = subRootId;
            GameObjectId = playerId;
            Permissions = perms;
            Stats = stats;
            GameMode = gameMode;
            LastStoredPosition = null;
            LastStoredSubRootID = Optional.Empty;
            UsedItems = new ThreadSafeList<NitroxTechType>(usedItems);
            QuickSlotsBindingIds = quickSlotsBindingIds;
            EquippedItems = new ThreadSafeDictionary<string, NitroxId>(equippedItems);
            visibleCells = new ThreadSafeSet<AbsoluteEntityCell>();
            PersonalCompletedGoalsWithTimestamp = new ThreadSafeDictionary<string, float>(personalCompletedGoalsWithTimestamp);
            PingInstancePreferences = new(pingInstancePreferences);
            PinnedRecipePreferences = new(pinnedRecipePreferences);
            InPrecursor = inPrecursor;
            DisplaySurfaceWater = displaySurfaceWater;
        }

        public static bool operator ==(Player? left, Player? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Player? left, Player? right)
        {
            return !Equals(left, right);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            if (obj.GetType() != GetType())
            {
                return false;
            }
            return Equals((Player)obj);
        }

        public override int GetHashCode()
        {
            return PeerId.GetHashCode();
        }

        public void AddCells(IEnumerable<AbsoluteEntityCell> cells)
        {
            foreach (AbsoluteEntityCell cell in cells)
            {
                visibleCells.Add(cell);
            }
        }

        public void RemoveCells(IEnumerable<AbsoluteEntityCell> cells)
        {
            foreach (AbsoluteEntityCell cell in cells)
            {
                visibleCells.Remove(cell);
            }
        }

        public bool HasCellLoaded(AbsoluteEntityCell cell)
        {
            return visibleCells.Contains(cell);
        }

        public void ClearVisibleCells()
        {
            visibleCells.Clear();
        }

        public bool CanSee(Entity entity)
        {
            if (entity is WorldEntity worldEntity)
            {
                return worldEntity is GlobalRootEntity || HasCellLoaded(worldEntity.AbsoluteEntityCell) ||
                       OutOfCellVisibleEntities.Contains(entity.Id);
            }

            return true;
        }

        /// <summary>
        ///     Where the player is now. Inside a base or Cyclops the body is held by the sub, so a Cyclops that moved since
        ///     the player last walked has taken the player along; <see cref="Position" /> only changes when the player
        ///     moves.
        /// </summary>
        public NitroxVector3 CurrentPosition => Entity?.Transform is { Parent: not null } transform ? transform.Position : Position;

        public void Teleport(NitroxVector3 destination, Optional<NitroxId> subRootID, IPacketSender packetSender)
        {
            PlayerTeleported playerTeleported = new(Name, Position, destination, subRootID);

            // "back" returns to where the player was, so it needs the sub the player was in, not the one of the destination
            LastStoredPosition = playerTeleported.DestinationFrom;
            LastStoredSubRootID = SubRootId;
            Position = playerTeleported.DestinationTo;
            packetSender.SendPacketAsync(playerTeleported, SessionId);
        }

        public override string ToString()
        {
            return $"[{nameof(Player)} - {nameof(PeerId)}: {PeerId}, {nameof(Name)}: {Name}, Perms: {Permissions}, {nameof(Position)}: {Position}]";
        }

        /// <summary>
        ///     Returns a <b>new</b> list from the original set. To use the original set, use <see cref="AddCells" />,
        ///     <see cref="RemoveCells" /> and <see cref="HasCellLoaded" />.
        /// </summary>
        internal List<AbsoluteEntityCell> GetVisibleCells()
        {
            return [.. visibleCells];
        }

        private bool Equals(Player other)
        {
            return PeerId == other.PeerId;
        }
    }
}
