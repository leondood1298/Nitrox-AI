using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Relays a player lying down in a bed and remembers it for players who join while they sleep.
/// </summary>
internal sealed class BedEnterAnimationProcessor(BedOccupancy bedOccupancy) : IAuthPacketProcessor<BedEnterAnimation>
{
    public async Task Process(AuthProcessorContext context, BedEnterAnimation packet)
    {
        // The session comes from the connection, not from what the packet claims.
        BedEnterAnimation announcement = bedOccupancy.SetLyingDown(context.Sender.SessionId, packet.BedId, packet.AnimationKey);
        await context.SendToOthersAsync(announcement);
    }
}
