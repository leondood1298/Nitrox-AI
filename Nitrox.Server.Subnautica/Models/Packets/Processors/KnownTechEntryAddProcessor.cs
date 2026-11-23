using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class KnownTechEntryAddProcessor(PdaManager pdaManager, PdaNotificationManager pdaNotificationManager) : IAuthPacketProcessor<KnownTechEntryAdd>
{
    private readonly PdaManager pdaManager = pdaManager;
    private readonly PdaNotificationManager pdaNotificationManager = pdaNotificationManager;

    public async Task Process(AuthProcessorContext context, KnownTechEntryAdd packet)
    {
        switch (packet.Category)
        {
            case KnownTechEntryAdd.EntryCategory.KNOWN:
                pdaManager.AddKnownTechType(packet.TechType, packet.CompletedScannerEntries);
                pdaNotificationManager.BlueprintLearned(packet.TechType, packet.Verbose);
                break;
            case KnownTechEntryAdd.EntryCategory.ANALYZED:
                pdaManager.AddAnalyzedTechType(packet.TechType);
                break;
        }

        await context.SendToOthersAsync(packet);
    }
}
