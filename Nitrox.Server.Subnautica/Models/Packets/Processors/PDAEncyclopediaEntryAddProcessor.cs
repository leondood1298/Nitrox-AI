using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class PDAEncyclopediaEntryAddProcessor(PdaManager pdaManager, PdaNotificationManager pdaNotificationManager) : IAuthPacketProcessor<PDAEncyclopediaEntryAdd>
{
    private readonly PdaManager pdaManager = pdaManager;
    private readonly PdaNotificationManager pdaNotificationManager = pdaNotificationManager;

    public async Task Process(AuthProcessorContext context, PDAEncyclopediaEntryAdd packet)
    {
        pdaManager.AddEncyclopediaEntry(packet.Key);
        pdaNotificationManager.EncyclopediaEntryAdded(packet.Key, packet.Verbose);
        await context.SendToOthersAsync(packet);
    }
}
