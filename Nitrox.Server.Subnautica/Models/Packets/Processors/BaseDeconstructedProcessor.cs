using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Bases;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class BaseDeconstructedProcessor(BuildingManager buildingManager, ScannerRoomManager scannerRoomManager) : BuildingProcessor<BaseDeconstructed>(buildingManager)
{
    public override async Task Process(AuthProcessorContext context, BaseDeconstructed packet)
    {
        if (BuildingManager.ReplaceBaseByGhost(packet))
        {
            await scannerRoomManager.BaseDeconstructedAsync(context.Sender, packet.FormerBaseId);
            await context.SendToOthersAsync(packet);
        }
    }
}
