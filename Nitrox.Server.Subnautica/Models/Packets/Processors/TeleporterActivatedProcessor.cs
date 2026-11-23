using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class TeleporterActivatedProcessor(ActiveTeleporters activeTeleporters) : IAuthPacketProcessor<TeleporterActivated>
{
    private readonly ActiveTeleporters activeTeleporters = activeTeleporters;

    public async Task Process(AuthProcessorContext context, TeleporterActivated packet)
    {
        // Every client that loads an open teleporter reports it; the others only need to hear of it once
        if (activeTeleporters.Add(packet.TeleporterIdentifier))
        {
            await context.SendToOthersAsync(packet);
        }
    }
}
