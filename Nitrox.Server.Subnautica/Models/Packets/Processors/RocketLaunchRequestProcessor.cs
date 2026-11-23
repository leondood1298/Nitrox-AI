using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class RocketLaunchRequestProcessor(RocketLaunchManager rocketLaunchManager) : IAuthPacketProcessor<RocketLaunchRequest>
{
    private readonly RocketLaunchManager rocketLaunchManager = rocketLaunchManager;

    public async Task Process(AuthProcessorContext context, RocketLaunchRequest packet)
    {
        await rocketLaunchManager.RequestLaunchAsync(context.Sender, packet);
    }
}
