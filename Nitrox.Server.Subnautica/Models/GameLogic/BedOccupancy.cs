using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.AppEvents;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Remembers which players are lying in which bed. Lying down is only announced once, when it happens, so a player
///     who joins afterwards would otherwise see the sleeper standing next to the bed.
/// </summary>
internal sealed class BedOccupancy : ISessionCleaner
{
    private readonly Dictionary<SessionId, BedEnterAnimation> lyingDownBySession = [];
    private readonly Lock occupancyLock = new();

    public BedEnterAnimation SetLyingDown(SessionId sessionId, NitroxId bedId, string animationKey)
    {
        BedEnterAnimation announcement = new(sessionId, bedId, animationKey);
        lock (occupancyLock)
        {
            lyingDownBySession[sessionId] = announcement;
        }
        return announcement;
    }

    public void SetStoodUp(SessionId sessionId)
    {
        lock (occupancyLock)
        {
            lyingDownBySession.Remove(sessionId);
        }
    }

    /// <summary>
    ///     What a player needs to be told in order to see everyone else who is currently in bed.
    /// </summary>
    public IReadOnlyList<BedEnterAnimation> GetAnnouncementsFor(SessionId joiningSessionId)
    {
        lock (occupancyLock)
        {
            return lyingDownBySession.Where(pair => pair.Key != joiningSessionId).Select(pair => pair.Value).ToList();
        }
    }

    public Task OnEventAsync(ISessionCleaner.Args args)
    {
        SetStoodUp(args.Session.Id);
        return Task.CompletedTask;
    }
}
