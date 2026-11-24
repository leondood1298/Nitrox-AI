using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

/// <summary>
///     Another player's seed took the planter slot this game planted in a moment before (see <see cref="Planters" />).
/// </summary>
internal sealed class PlanterSlotTakenProcessor(Planters planters) : IClientPacketProcessor<PlanterSlotTaken>
{
    private readonly Planters planters = planters;

    public Task Process(ClientProcessorContext context, PlanterSlotTaken packet)
    {
        planters.SlotTaken(packet.PlantableId, packet.SlotId);
        return Task.CompletedTask;
    }
}
