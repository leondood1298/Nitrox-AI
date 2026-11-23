using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class DeathMarkerReachedProcessor : IAuthPacketProcessor<DeathMarkerReached>
{
    public Task Process(AuthProcessorContext context, DeathMarkerReached packet)
    {
        PlayerDeaths.ForgetMarker(context.Sender, packet.Position);
        return Task.CompletedTask;
    }
}
