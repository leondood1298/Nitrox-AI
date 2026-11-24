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
    private readonly HashSet<SessionId> weldingSessions = [];
    private readonly Dictionary<SessionId, NitroxId> propulsionGrabBySession = [];
    private readonly Dictionary<SessionId, PlayerCinematicControllerCall> cinematicBySession = [];
    private readonly Lock statesLock = new();

    /// <summary>
    ///     A welder in use shows sparks, which a joiner would otherwise not see until the player stops and starts again.
    /// </summary>
    public void OnWelderUse(SessionId sessionId, bool welding)
    {
        lock (statesLock)
        {
            if (welding)
            {
                weldingSessions.Add(sessionId);
            }
            else
            {
                weldingSessions.Remove(sessionId);
            }
        }
    }

    public void OnPropulsionGrab(SessionId sessionId, NitroxId? grabbedId)
    {
        lock (statesLock)
        {
            if (grabbedId != null)
            {
                propulsionGrabBySession[sessionId] = grabbedId;
            }
            else
            {
                propulsionGrabBySession.Remove(sessionId);
            }
        }
    }

    /// <summary>
    ///     A cinematic under way (a ladder, a hatch, a terminal) is played for a joiner from its start; its end then follows
    ///     as for everyone.
    /// </summary>
    public void OnCinematic(SessionId sessionId, PlayerCinematicControllerCall call)
    {
        lock (statesLock)
        {
            if (call.StartPlaying)
            {
                cinematicBySession[sessionId] = call;
            }
            else
            {
                cinematicBySession.Remove(sessionId);
            }
        }
    }

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
                        // A tool put away is no longer welding or holding anything
                        weldingSessions.Remove(sessionId);
                        propulsionGrabBySession.Remove(sessionId);
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
            Forget(sessionId);
        }
    }

    private void Forget(SessionId sessionId)
    {
        drawnBySession.Remove(sessionId);
        benchBySession.Remove(sessionId);
        weldingSessions.Remove(sessionId);
        propulsionGrabBySession.Remove(sessionId);
        cinematicBySession.Remove(sessionId);
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
            // After the held items, so that the welder or cannon is in the hand when its use is shown
            foreach (SessionId welding in weldingSessions)
            {
                if (welding != joiningSessionId)
                {
                    announcements.Add(new WelderUse(welding, true));
                }
            }
            foreach (KeyValuePair<SessionId, NitroxId> pair in propulsionGrabBySession)
            {
                if (pair.Key != joiningSessionId)
                {
                    announcements.Add(new PlayerPropulsionGrab(pair.Key, pair.Value));
                }
            }
            foreach (KeyValuePair<SessionId, NitroxId> pair in benchBySession)
            {
                if (pair.Key != joiningSessionId)
                {
                    announcements.Add(new BenchChanged(pair.Key, pair.Value, Nitrox.Model.Subnautica.Packets.BenchChanged.BenchChangeState.SITTING_DOWN));
                }
            }
            foreach (KeyValuePair<SessionId, PlayerCinematicControllerCall> pair in cinematicBySession)
            {
                if (pair.Key != joiningSessionId)
                {
                    announcements.Add(pair.Value);
                }
            }
        }
        return announcements;
    }

    public Task OnEventAsync(ISessionCleaner.Args args)
    {
        lock (statesLock)
        {
            Forget(args.Session.Id);
        }
        return Task.CompletedTask;
    }
}
