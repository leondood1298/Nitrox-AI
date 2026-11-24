using System;

namespace NitroxClient.Communication.MultiplayerSession;

/// <summary>
///     How long a joining game waits for the server's reply at each step before it gives up. A server answers both
///     requests at once; one that stays silent this long is hung or gone, and the player would otherwise wait forever.
/// </summary>
public static class JoinReplyTimeout
{
    public static readonly TimeSpan POLICY_REPLY = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan RESERVATION_REPLY = TimeSpan.FromSeconds(20);

    /// <returns>The time allowed at that step, or null where the game waits on the player, not the server.</returns>
    public static TimeSpan? For(MultiplayerSessionConnectionStage stage) => stage switch
    {
        MultiplayerSessionConnectionStage.ESTABLISHING_SERVER_POLICY => POLICY_REPLY,
        MultiplayerSessionConnectionStage.AWAITING_SESSION_RESERVATION => RESERVATION_REPLY,
        _ => null
    };
}
