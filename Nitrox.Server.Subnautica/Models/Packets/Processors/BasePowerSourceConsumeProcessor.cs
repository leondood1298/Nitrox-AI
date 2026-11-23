using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Routes energy that a player consumed from a base power source to whoever simulates that source. Consumers such
///     as a docked vehicle, a battery charger or a Scanner Room run on the player simulating the consumer, which is
///     often not the player simulating the reactor it draws from.
/// </summary>
internal sealed class BasePowerSourceConsumeProcessor(EntityRegistry entityRegistry, BasePowerManager basePowerManager, BasePowerDiagnostics diagnostics,
    ILogger<BasePowerSourceConsumeProcessor> logger) : IAuthPacketProcessor<BasePowerSourceConsume>
{
    public async Task Process(AuthProcessorContext context, BasePowerSourceConsume packet)
    {
        if (!entityRegistry.TryGetEntityById(packet.SourceId, out Entity entity))
        {
            logger.ZLogWarningOnce($"[BasePower] {context.Sender.Name} #{context.Sender.SessionId} consumed from unknown source {packet.SourceId}");
            return;
        }

        ConsumptionRoute route = basePowerManager.RouteConsumption(context.Sender, entity, packet.Amount, out Player? simulator, out PowerSourceMetadata? accepted);
        switch (route)
        {
            case ConsumptionRoute.FORWARD_TO_SIMULATOR:
                await context.SendAsync(packet, simulator!.SessionId);
                break;
            case ConsumptionRoute.APPLIED_BY_SERVER:
                // Nobody simulates the source, so nobody else will tell the players its new level
                await context.SendToAllAsync(new EntityMetadataUpdate(packet.SourceId, accepted!));
                break;
            default:
                return;
        }

        if (diagnostics.TraceEnabled)
        {
            logger.ZLogInformation($"[BasePower] {context.Sender.Name} #{context.Sender.SessionId} consumed {packet.Amount:F3} from source {packet.SourceId}: {route}");
        }
    }
}
