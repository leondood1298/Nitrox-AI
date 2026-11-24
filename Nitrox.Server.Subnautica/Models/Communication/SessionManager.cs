using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using Nitrox.Model.Core;
using Nitrox.Server.Subnautica.Models.AppEvents;

namespace Nitrox.Server.Subnautica.Models.Communication;

/// <summary>
///     Assigns session ids to player connections. Each session id is unique for the current server instance.
///     A session id will be reused when connections are lost. It will take at least 10 minutes before a session id is
///     reused to prevent impersonation and other bugs.
/// </summary>
internal sealed class SessionManager(ISessionCleaner.Trigger sessionCleanTrigger, ILogger<SessionManager> logger)
{
    private readonly ILogger<SessionManager> logger = logger;
    private readonly Queue<(TimeSpan ReturnedTimeStamp, SessionId Id)> returnedSessionIds = [];
    private readonly ISessionCleaner.Trigger sessionCleanTrigger = sessionCleanTrigger;
    private readonly Dictionary<EndpointKey, SessionId> sessionIdByEndpoint = [];
    private readonly Lock sessionLock = new();
    private readonly Dictionary<SessionId, Session> sessions = [];
    private readonly Stopwatch time = Stopwatch.StartNew();

    /// <summary>
    ///     The next session id. Returns a prior id if <see cref="returnedSessionIds" /> are available.
    /// </summary>
    /// <exception cref="InvalidOperationException">Every id is in use or waiting to be reused.</exception>
    private SessionId NextSessionId
    {
        get
        {
            lock (sessionLock)
            {
                if (returnedSessionIds.Count > 0 && returnedSessionIds.TryPeek(out (TimeSpan ReturnedTimeStamp, SessionId Id) entry) && (time.Elapsed - entry.ReturnedTimeStamp).TotalMinutes >= SessionId.DELAY_REUSE_MINUTES)
                {
                    SessionId reused = returnedSessionIds.Dequeue().Id;
                    waitingSessionIds.Remove(reused);
                    return reused;
                }
                // The counter wraps after 65535 connections, which a script can open within the reuse delay. Wrapping
                // must neither hand out 0 (the server's own id) nor an id still in use or still waiting out its delay.
                for (int tries = 0; tries < ushort.MaxValue; tries++)
                {
                    ushort next = (ushort)(nextFreshId % ushort.MaxValue + 1);
                    nextFreshId = next;
                    SessionId candidate = next;
                    if (!sessions.ContainsKey(candidate) && !waitingSessionIds.Contains(candidate))
                    {
                        return candidate;
                    }
                }
            }
            throw new InvalidOperationException("No session id is free");
        }
    }

    private ushort nextFreshId;

    /// <summary>
    ///     The ids in <see cref="returnedSessionIds" />, to look them up without walking the queue.
    /// </summary>
    private readonly HashSet<SessionId> waitingSessionIds = [];

    public Session GetOrCreateSession(IPEndPoint endPoint)
    {
        Session session;
        EndpointKey key = ToKey(endPoint);
        bool created = false;
        lock (sessionLock)
        {
            if (!sessionIdByEndpoint.TryGetValue(key, out SessionId id))
            {
                id = NextSessionId;
                if (sessionIdByEndpoint.TryAdd(key, id))
                {
                    session = new Session(id, endPoint);
                    sessions.Add(id, session);
                    created = true;
                }
            }

            session = sessions[id];
        }
        if (created)
        {
            logger.ZLogInformation($"Created session #{session.Id} for connection {endPoint}");
        }
        return session;
    }

    public IPEndPoint? GetEndPoint(SessionId sessionId)
    {
        lock (sessionLock)
        {
            sessions.TryGetValue(sessionId, out Session session);
            return session?.EndPoint;
        }
    }

    public bool IsConnected(SessionId sessionId)
    {
        lock (sessionLock)
        {
            return sessions.ContainsKey(sessionId);
        }
    }

    public async Task<bool> RemoveSessionAsync(SessionId sessionId)
    {
        Session session;
        int sessionCountAfter;
        lock (sessionLock)
        {
            if (!sessions.Remove(sessionId, out session))
            {
                return false;
            }
            sessionCountAfter = sessions.Count;
            sessionIdByEndpoint.Remove(ToKey(session.EndPoint));
            returnedSessionIds.Enqueue((time.Elapsed, session.Id));
            waitingSessionIds.Add(session.Id);
        }
        logger.ZLogTrace($"Removing session #{sessionId}");
        await sessionCleanTrigger.InvokeAsync(new ISessionCleaner.Args(session, sessionCountAfter));
        logger.ZLogTrace($"Removed session #{sessionId}");
        return true;
    }

    public int GetSessionCount()
    {
        lock (sessionLock)
        {
            return sessions.Count;
        }
    }

    private EndpointKey ToKey(IPEndPoint endPoint) => new(endPoint.Address, (ushort)endPoint.Port);

    public record Session(SessionId Id, IPEndPoint EndPoint);

    private record EndpointKey(IPAddress Address, ushort Port);
}
