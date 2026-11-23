using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Core;
using NitroxClient.GameLogic.HUD;
using NitroxClient.GameLogic.PlayerLogic.PlayerModel;
using NitroxClient.MonoBehaviours.Discord;
using Nitrox.Model.DataStructures;
using Nitrox.Model.GameLogic.FMOD;
using Nitrox.Model.Helper;
using Nitrox.Model.Subnautica.MultiplayerSession;
using UnityEngine;

namespace NitroxClient.GameLogic;

public class PlayerManager
{
    private readonly PlayerModelManager playerModelManager;
    private readonly PlayerVitalsManager playerVitalsManager;
    private readonly FMODWhitelist fmodWhitelist;
    private readonly Dictionary<SessionId, RemotePlayer> sessionsById = new();

    public OnCreateDelegate OnCreate;
    public OnRemoveDelegate OnRemove;

    /// <summary>
    ///     A remote player's body exists and carries their id, some time after <see cref="OnCreate" />.
    /// </summary>
    public OnBodyCreatedDelegate OnBodyCreated;

    public PlayerManager(PlayerModelManager playerModelManager, PlayerVitalsManager playerVitalsManager, FMODWhitelist fmodWhitelist)
    {
        this.playerModelManager = playerModelManager;
        this.playerVitalsManager = playerVitalsManager;
        this.fmodWhitelist = fmodWhitelist;
    }

    public Optional<RemotePlayer> Find(SessionId sessionId)
    {
        sessionsById.TryGetValue(sessionId, out RemotePlayer player);
        return Optional.OfNullable(player);
    }

    public bool TryFind(SessionId sessionId, out RemotePlayer remotePlayer) => sessionsById.TryGetValue(sessionId, out remotePlayer);

    public Optional<RemotePlayer> Find(NitroxId playerNitroxId)
    {
        RemotePlayer remotePlayer = sessionsById.Select(idToPlayer => idToPlayer.Value)
                                               .FirstOrDefault(player => player.PlayerContext.PlayerNitroxId == playerNitroxId);

        return Optional.OfNullable(remotePlayer);
    }

    public IEnumerable<RemotePlayer> GetAll()
    {
        return sessionsById.Values;
    }

    public HashSet<GameObject> GetAllPlayerObjects()
    {
        HashSet<GameObject> remotePlayerObjects = GetAll()
                                                  .Select(player => player.Body)
                                                  .Where(body => body != null)
                                                  .ToSet();
        if (Player.mainObject != null)
        {
            remotePlayerObjects.Add(Player.mainObject);
        }
        return remotePlayerObjects;
    }

    public RemotePlayer Create(PlayerContext playerContext)
    {
        Validate.NotNull(playerContext);

        // Can happen that player is already known if both join queue & initial sync happen.
        if (sessionsById.TryGetValue(playerContext.SessionId, out RemotePlayer alreadyAddedPlayer))
        {
            return alreadyAddedPlayer;
        }

        RemotePlayer remotePlayer = new(playerContext, playerModelManager, playerVitalsManager, fmodWhitelist);

        sessionsById.Add(remotePlayer.SessionId, remotePlayer);
        OnCreate(remotePlayer.SessionId, remotePlayer);

        DiscordClient.UpdatePartySize(GetTotalPlayerCount());

        return remotePlayer;
    }

    public void BodyCreated(RemotePlayer remotePlayer)
    {
        OnBodyCreated?.Invoke(remotePlayer);
    }

    public void RemovePlayer(SessionId sessionId)
    {
        if (sessionsById.TryGetValue(sessionId, out RemotePlayer player))
        {
            player.Destroy();
            sessionsById.Remove(sessionId);
            OnRemove(sessionId, player);
            DiscordClient.UpdatePartySize(GetTotalPlayerCount());
        }
    }

    /// <summary>
    ///     The hand of the remote player nearest to <paramref name="position" /> who holds a tool of type
    ///     <typeparamref name="T" />.
    /// </summary>
    /// <returns>Null when no remote player holds such a tool.</returns>
    public Transform? FindNearestHandHolding<T>(Vector3 position) where T : PlayerTool
    {
        Transform? nearestHand = null;
        float nearestDistance = float.MaxValue;
        foreach (RemotePlayer player in sessionsById.Values)
        {
            // A drawn tool hangs from the attach point; a holstered one is moved back into the inventory
            if (!player.ItemAttachPoint || !player.ItemAttachPoint.GetComponentInChildren<T>())
            {
                continue;
            }
            float distance = Vector3.Distance(position, player.ItemAttachPoint.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestHand = player.ItemAttachPoint;
            }
        }
        return nearestHand;
    }

    /// <returns>Remote players + You => X + 1</returns>
    public int GetTotalPlayerCount() => sessionsById.Count + 1;

    public delegate void OnCreateDelegate(SessionId sessionId, RemotePlayer remotePlayer);
    public delegate void OnRemoveDelegate(SessionId sessionId, RemotePlayer remotePlayer);
    public delegate void OnBodyCreatedDelegate(RemotePlayer remotePlayer);
}
