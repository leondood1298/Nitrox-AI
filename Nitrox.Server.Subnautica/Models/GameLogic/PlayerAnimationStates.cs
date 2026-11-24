using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Core;
using Nitrox.Model.GameLogic.PlayerAnimation;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.AppEvents;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Remembers which lasting animations each player has switched on (holding the PDA up, using a tool). A change is
///     only announced when it happens, so a player who joins afterwards has to be told what is already on.
/// </summary>
internal sealed class PlayerAnimationStates : ISessionCleaner
{
    private readonly Dictionary<SessionId, HashSet<AnimChangeType>> switchedOnBySession = [];
    private readonly Lock statesLock = new();

    /// <summary>
    ///     Whether an animation stays as it is until the player changes it again. Being underwater also does, but joining
    ///     players get that with the rest of the player's details. The others are over within moments.
    /// </summary>
    public static bool IsLasting(AnimChangeType type) => type is AnimChangeType.USING_PDA or AnimChangeType.USING_TOOL or AnimChangeType.USING_TOOL_ALT or AnimChangeType.BLEEDER_ATTACHED
                                                                    or AnimChangeType.ON_SURFACE or AnimChangeType.STASIS_RIFLE_CHARGED or AnimChangeType.DIVE_REEL_OUT_OF_AMMO
                                                                    or AnimChangeType.EXOSUIT_USE_LEFT or AnimChangeType.EXOSUIT_USE_RIGHT;

    public void Set(SessionId sessionId, PlayerAnimation animation)
    {
        if (!IsLasting(animation.Type))
        {
            return;
        }

        lock (statesLock)
        {
            if (animation.State == AnimChangeState.ON)
            {
                if (!switchedOnBySession.TryGetValue(sessionId, out HashSet<AnimChangeType> switchedOn))
                {
                    switchedOnBySession[sessionId] = switchedOn = [];
                }
                switchedOn.Add(animation.Type);
            }
            else if (switchedOnBySession.TryGetValue(sessionId, out HashSet<AnimChangeType> switchedOn))
            {
                switchedOn.Remove(animation.Type);
            }
        }
    }

    /// <summary>
    ///     What a player needs to be told in order to see what everyone else is doing right now.
    /// </summary>
    public IReadOnlyList<AnimationChangeEvent> GetAnnouncementsFor(SessionId joiningSessionId)
    {
        lock (statesLock)
        {
            return switchedOnBySession.Where(pair => pair.Key != joiningSessionId)
                                      .SelectMany(pair => pair.Value.Select(type => new AnimationChangeEvent(pair.Key, new PlayerAnimation(type, AnimChangeState.ON))))
                                      .ToList();
        }
    }

    public Task OnEventAsync(ISessionCleaner.Args args)
    {
        lock (statesLock)
        {
            switchedOnBySession.Remove(args.Session.Id);
        }
        return Task.CompletedTask;
    }
}
