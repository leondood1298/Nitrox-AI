using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Server.Subnautica.Models.AppEvents;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

internal sealed class MapRoomScanResultSubscriptions : ISessionCleaner
{
    private readonly ConcurrentDictionary<(NitroxId MapRoomId, SessionId SessionId), byte> subscribers = new();

    public void Set(NitroxId mapRoomId, SessionId sessionId, bool subscribed)
    {
        if (subscribed)
        {
            subscribers.TryAdd((mapRoomId, sessionId), 0);
        }
        else
        {
            subscribers.TryRemove((mapRoomId, sessionId), out _);
        }
    }

    public bool Contains(NitroxId mapRoomId, SessionId sessionId)
    {
        return subscribers.ContainsKey((mapRoomId, sessionId));
    }

    public List<SessionId> RemoveRoom(NitroxId mapRoomId)
    {
        List<SessionId> removed = subscribers.Keys.Where(key => key.MapRoomId == mapRoomId).Select(key => key.SessionId).ToList();
        foreach (SessionId sessionId in removed)
        {
            subscribers.TryRemove((mapRoomId, sessionId), out _);
        }
        return removed;
    }

    /// <summary>
    ///     Session ids are handed out again after a delay, so a departed player's subscriptions must not outlive the session.
    /// </summary>
    public void RemoveSession(SessionId sessionId)
    {
        foreach ((NitroxId MapRoomId, SessionId SessionId) key in subscribers.Keys.Where(key => key.SessionId == sessionId).ToList())
        {
            subscribers.TryRemove(key, out _);
        }
    }

    public Task OnEventAsync(ISessionCleaner.Args args)
    {
        RemoveSession(args.Session.Id);
        return Task.CompletedTask;
    }
}
