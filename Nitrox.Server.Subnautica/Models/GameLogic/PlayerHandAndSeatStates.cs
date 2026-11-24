using System.Collections.Generic;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.AppEvents;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Remembers what each player holds in their hand and which bench they sit on. Both are only announced when they
///     change, so a player who joins afterwards saw everyone empty-handed and standing.
/// </summary>
internal sealed class PlayerHandAndSeatStates : ISessionCleaner
{
    private readonly Dictionary<SessionId, PlayerHeldItemChanged> drawnBySession = [];
    private readonly Dictionary<SessionId, NitroxId> benchBySession = [];
    private readonly Lock statesLock = new();

    public void OnHeldItemChanged(SessionId sessionId, PlayerHeldItemChanged change)
    {
        lock (statesLock)
        {
            switch (change.Type)
            {
                case PlayerHeldItemChanged.ChangeType.DRAW_AS_TOOL:
                case PlayerHeldItemChanged.ChangeType.DRAW_AS_ITEM:
                    // A joiner is told without the first-use flag: that animation is over by now
                    drawnBySession[sessionId] = new PlayerHeldItemChanged(sessionId, change.ItemId, change.Type, null);
                    break;
                default:
                    if (drawnBySession.TryGetValue(sessionId, out PlayerHeldItemChanged drawn) && drawn.ItemId.Equals(change.ItemId))
                    {
                        drawnBySession.Remove(sessionId);
                    }
                    break;
            }
        }
    }

    public void OnBenchChanged(SessionId sessionId, BenchChanged change)
    {
        lock (statesLock)
        {
            if (change.ChangeState == Nitrox.Model.Subnautica.Packets.BenchChanged.BenchChangeState.SITTING_DOWN)
            {
                benchBySession[sessionId] = change.BenchId;
            }
            else
            {
                benchBySession.Remove(sessionId);
            }
        }
    }

    /// <summary>
    ///     A player who dies drops out of the bench and loses what was in their hand.
    /// </summary>
    public void PlayerDied(SessionId sessionId)
    {
        lock (statesLock)
        {
            drawnBySession.Remove(sessionId);
            benchBySession.Remove(sessionId);
        }
    }

    /// <summary>
    ///     What a player needs to be told, once its world is set up, to see what everyone else holds and where they sit.
    /// </summary>
    public IReadOnlyList<Packet> GetAnnouncementsFor(SessionId joiningSessionId)
    {
        List<Packet> announcements = [];
        lock (statesLock)
        {
            foreach (KeyValuePair<SessionId, PlayerHeldItemChanged> pair in drawnBySession)
            {
                if (pair.Key != joiningSessionId)
                {
                    announcements.Add(pair.Value);
                }
            }
            foreach (KeyValuePair<SessionId, NitroxId> pair in benchBySession)
            {
                if (pair.Key != joiningSessionId)
                {
                    announcements.Add(new BenchChanged(pair.Key, pair.Value, Nitrox.Model.Subnautica.Packets.BenchChanged.BenchChangeState.SITTING_DOWN));
                }
            }
        }
        return announcements;
    }

    public Task OnEventAsync(ISessionCleaner.Args args)
    {
        lock (statesLock)
        {
            drawnBySession.Remove(args.Session.Id);
            benchBySession.Remove(args.Session.Id);
        }
        return Task.CompletedTask;
    }
}
