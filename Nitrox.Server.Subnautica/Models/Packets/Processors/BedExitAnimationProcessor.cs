using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Relays a player getting out of bed and stops telling joining players that they are lying in it.
/// </summary>
internal sealed class BedExitAnimationProcessor(BedOccupancy bedOccupancy) : IAuthPacketProcessor<BedExitAnimation>
{
    public async Task Process(AuthProcessorContext context, BedExitAnimation packet)
    {
        bedOccupancy.SetStoodUp(context.Sender.SessionId);
        await context.SendToOthersAsync(new BedExitAnimation(context.Sender.SessionId, packet.BedId, packet.AnimationKey));
    }
}
