using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace NitroxClient.Communication;

/// <summary>
///     Which LAN servers still answer. A running server answers every search broadcast; one that has not answered for
///     <see cref="EXPIRY" /> has stopped (or left the network) and leaves the list.
/// </summary>
public sealed class LanServerTracker
{
    /// <summary>
    ///     Several missed broadcasts (one every 3 seconds), so one lost packet does not make a server blink out.
    /// </summary>
    public static readonly TimeSpan EXPIRY = TimeSpan.FromSeconds(10);

    private readonly Dictionary<IPEndPoint, DateTime> lastAnswerByServer = [];

    public IEnumerable<IPEndPoint> Known => lastAnswerByServer.Keys;

    /// <returns>True when this server was not known yet.</returns>
    public bool Answered(IPEndPoint server, DateTime now)
    {
        bool isNew = !lastAnswerByServer.ContainsKey(server);
        lastAnswerByServer[server] = now;
        return isNew;
    }

    /// <summary>
    ///     Removes and returns the servers that stopped answering.
    /// </summary>
    public List<IPEndPoint> TakeSilent(DateTime now)
    {
        List<IPEndPoint> silent = lastAnswerByServer.Where(pair => now - pair.Value > EXPIRY).Select(pair => pair.Key).ToList();
        foreach (IPEndPoint server in silent)
        {
            lastAnswerByServer.Remove(server);
        }
        return silent;
    }

    /// <summary>
    ///     While nobody asked (the list was closed) silence means nothing: every known server gets a fresh chance to answer.
    /// </summary>
    public void RestartWaiting(DateTime now)
    {
        foreach (IPEndPoint server in lastAnswerByServer.Keys.ToList())
        {
            lastAnswerByServer[server] = now;
        }
    }

    public void Forget(IPEndPoint server) => lastAnswerByServer.Remove(server);

    public void Clear() => lastAnswerByServer.Clear();
}
