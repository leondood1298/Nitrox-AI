using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

internal sealed class MapRoomScanResultSubscriptions
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
}
