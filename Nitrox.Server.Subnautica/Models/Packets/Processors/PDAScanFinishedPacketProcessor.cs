using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class PDAScanFinishedPacketProcessor(PdaManager pdaManager, WorldEntityManager worldEntityManager, ScannerRoomManager scannerRoomManager) : IAuthPacketProcessor<PDAScanFinished>
{
    private readonly PdaManager pdaManager = pdaManager;
    private readonly WorldEntityManager worldEntityManager = worldEntityManager;

    public async Task Process(AuthProcessorContext context, PDAScanFinished packet)
    {
        if (packet.WasAlreadyResearched)
        {
            await context.SendToOthersAsync(packet);
        }
        else
        {
            int unlocked = pdaManager.AddScannedFragment(packet.TechType, packet.TotalFragments, out bool fullyResearched);
            // The scanning player gets the count too: its game counted only its own scan
            await context.SendToAllAsync(new PDAScanFinished(packet.Id, packet.TechType, unlocked, packet.TotalFragments, fullyResearched, packet.Destroy));
        }

        if (packet.Id != null)
        {
            if (packet.Destroy)
            {
                await scannerRoomManager.EntityRemovedAsync(context.Sender, packet.Id);
                worldEntityManager.TryDestroyEntity(packet.Id, out _);
            }
            else
            {
                pdaManager.AddScannerFragment(packet.Id);
            }
        }
    }
}
