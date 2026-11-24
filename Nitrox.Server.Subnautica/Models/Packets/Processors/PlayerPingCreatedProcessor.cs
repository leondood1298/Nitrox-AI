using Nitrox.Model.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Relays a player's ping as coming from that player, whatever session the packet named, with its texts bounded. A
///     muted player's pings and pings beyond a small burst (see <see cref="PlayerPingLimiter" />) are not relayed.
/// </summary>
internal sealed class PlayerPingCreatedProcessor(PlayerPingLimiter limiter) : IAuthPacketProcessor<PlayerPingCreated>
{
    /// <summary>
    ///     Longer than any tech type or object name the game has.
    /// </summary>
    public const int MAX_TEXT_LENGTH = 64;

    private readonly PlayerPingLimiter limiter = limiter;

    public async Task Process(AuthProcessorContext context, PlayerPingCreated packet)
    {
        // A ping carries a text chosen by its sender, as chat does
        if (context.Sender.PlayerContext?.IsMuted == true || !limiter.TryPing(context.Sender.SessionId))
        {
            return;
        }
        await context.SendToOthersAsync(Sanitize(packet, context.Sender.SessionId));
    }

    internal static PlayerPingCreated Sanitize(PlayerPingCreated packet, SessionId senderSessionId)
    {
        return new PlayerPingCreated(senderSessionId, Cap(packet.TargetTechType), Cap(packet.TargetName), packet.Position, packet.PingId);
    }

    private static string Cap(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }
        return text.Length > MAX_TEXT_LENGTH ? text[..MAX_TEXT_LENGTH] : text;
    }
}
