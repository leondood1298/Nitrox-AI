using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;

namespace Nitrox.Server.Subnautica.Models.GameLogic
{
    internal sealed class SimulationOwnershipData
    {
        public struct PlayerLock
        {
            public Player Player { get; }
            public SimulationLockType LockType { get; set; }

            public PlayerLock(Player player, SimulationLockType lockType)
            {
                Player = player;
                LockType = lockType;
            }
        }

        public readonly record struct RevokedLock(NitroxId EntityId, PlayerLock Lock);

        Dictionary<NitroxId, PlayerLock> playerLocksById = new Dictionary<NitroxId, PlayerLock>();

        public bool TryToAcquire(NitroxId id, Player player, SimulationLockType requestedLock)
        {
            lock (playerLocksById)
            {
                // If no one is simulating then acquire a lock for this player
                if (!playerLocksById.TryGetValue(id, out PlayerLock playerLock))
                {
                    playerLocksById[id] = new PlayerLock(player, requestedLock);
                    return true;
                }

                // If this player owns the lock then they are already simulating
                if (playerLock.Player == player)
                {
                    // update the lock type in case they are attempting to downgrade
                    playerLocksById[id] = new PlayerLock(player, requestedLock);
                    return true;
                }

                // If the current lock owner has a transient lock then only override if we are requesting exclusive access
                if (playerLock.LockType == SimulationLockType.TRANSIENT && requestedLock == SimulationLockType.EXCLUSIVE)
                {
                    playerLocksById[id] = new PlayerLock(player, requestedLock);
                    return true;
                }

                // We must be requesting a transient lock and the owner already has a lock (either transient or exclusive).
                // there is no way to break it so we will return false.
                return false;
            }
        }

        public bool RevokeIfOwner(NitroxId id, Player player)
        {
            return RevokeIfOwner(id, player, out _);
        }

        public bool RevokeIfOwner(NitroxId id, Player player, out PlayerLock revokedLock)
        {
            lock (playerLocksById)
            {
                if (playerLocksById.TryGetValue(id, out PlayerLock playerLock) && playerLock.Player == player)
                {
                    playerLocksById.Remove(id);
                    revokedLock = playerLock;
                    return true;
                }

                revokedLock = default;
                return false;
            }
        }

        public List<NitroxId> RevokeAllForOwner(SessionId sessionId)
        {
            return RevokeAllLocksForOwner(sessionId).Select(revoked => revoked.EntityId).ToList();
        }

        public List<RevokedLock> GetLocksForOwner(SessionId sessionId)
        {
            lock (playerLocksById)
            {
                return playerLocksById
                       .Where(entry => entry.Value.Player.SessionId == sessionId)
                       .Select(entry => new RevokedLock(entry.Key, entry.Value))
                       .ToList();
            }
        }

        public List<RevokedLock> RevokeAllLocksForOwner(SessionId sessionId)
        {
            lock (playerLocksById)
            {
                List<RevokedLock> revokedLocks = [];

                foreach (KeyValuePair<NitroxId, PlayerLock> idWithPlayerLock in playerLocksById)
                {
                    if (idWithPlayerLock.Value.Player.SessionId == sessionId)
                    {
                        revokedLocks.Add(new RevokedLock(idWithPlayerLock.Key, idWithPlayerLock.Value));
                    }
                }

                foreach (RevokedLock revokedLock in revokedLocks)
                {
                    playerLocksById.Remove(revokedLock.EntityId);
                }

                return revokedLocks;
            }
        }

        public bool RevokeOwnerOfId(NitroxId id)
        {
            return RevokeOwnerOfId(id, out _);
        }

        public bool RevokeOwnerOfId(NitroxId id, out PlayerLock revokedLock)
        {
            lock (playerLocksById)
            {
                if (playerLocksById.Remove(id, out PlayerLock playerLock))
                {
                    revokedLock = playerLock;
                    return true;
                }
                revokedLock = default;
                return false;
            }
        }

        public Player? GetPlayerForLock(NitroxId id)
        {
            lock (playerLocksById)
            {
                if (playerLocksById.TryGetValue(id, out PlayerLock playerLock))
                {
                    return playerLock.Player;
                }
            }
            return null;
        }

        public bool TryGetLock(NitroxId id, out PlayerLock playerLock)
        {
            lock (playerLocksById)
            {
                return playerLocksById.TryGetValue(id, out playerLock);
            }
        }

        /// <summary>
        ///     Runs an authority-sensitive transition while ownership cannot be revoked or
        ///     reassigned. The callback must remain short; it may synchronously enqueue a packet
        ///     but must not block waiting for external work.
        /// </summary>
        public TResult ExecuteForOwner<TResult>(Player player, IEnumerable<NitroxId> ids,
            Func<HashSet<NitroxId>, TResult> action)
        {
            lock (playerLocksById)
            {
                HashSet<NitroxId> ownedIds = ids
                    .Where(id => playerLocksById.TryGetValue(id, out PlayerLock playerLock) && playerLock.Player == player)
                    .ToHashSet();
                return action(ownedIds);
            }
        }
    }
}
