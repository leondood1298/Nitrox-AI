using System.Collections.Generic;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxClient.MonoBehaviours;

public class RemotePlayerBenchBlocker : MonoBehaviour, IConstructable
{
    private readonly HashSet<RemotePlayer> remoteSittingPlayer = [];

    /// <summary>
    ///     Players whose leaving or dying already frees this bench. Handlers are not removed again: that could happen
    ///     while the event runs them.
    /// </summary>
    private readonly HashSet<RemotePlayer> watchedPlayers = [];

    public void AddPlayerToBench(RemotePlayer remotePlayer)
    {
        remoteSittingPlayer.Add(remotePlayer);
        // A player who leaves or dies while seated never stands up; the bench must not stay blocked
        if (watchedPlayers.Add(remotePlayer))
        {
            remotePlayer.PlayerDisconnectEvent.AddHandler(gameObject, RemovePlayerFromBench);
            remotePlayer.PlayerDeathEvent.AddHandler(gameObject, RemovePlayerFromBench);
        }
    }

    public void RemovePlayerFromBench(RemotePlayer remotePlayer) => remoteSittingPlayer.Remove(remotePlayer);

    public bool IsDeconstructionObstacle() => true;

    public void OnConstructedChanged(bool constructed) {}

    public bool CanDeconstruct(out string reason)
    {
        if (remoteSittingPlayer.Count != 0)
        {
            reason = Language.main.Get("Nitrox_RemotePlayerObstacle");
            return false;
        }
        reason = string.Empty;
        return true;
    }
}
