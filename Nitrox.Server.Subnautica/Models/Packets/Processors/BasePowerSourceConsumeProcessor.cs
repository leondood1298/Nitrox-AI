using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Routes energy that a player consumed from a base power source to whoever simulates that source. Consumers such
///     as a docked vehicle, a battery charger or a Scanner Room run on the player simulating the consumer, which is
///     often not the player simulating the reactor it draws from.
/// </summary>
internal sealed class BasePowerSourceConsumeProcessor(EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData,
    BasePowerSourceAuthority authority, BasePowerDiagnostics diagnostics, ILogger<BasePowerSourceConsumeProcessor> logger) : IAuthPacketProcessor<BasePowerSourceConsume>
{
    public async Task Process(AuthProcessorContext context, BasePowerSourceConsume packet)
    {
        if (!entityRegistry.TryGetEntityById(packet.SourceId, out Entity entity))
        {
            diagnostics.RecordConsumption(packet.SourceId, context.Sender.SessionId, null, packet.Amount, ConsumptionRoute.REJECTED, null, "unknown source");
            return;
        }

        Player? owner = simulationOwnershipData.GetPlayerForLock(packet.SourceId);
        ConsumptionRoute route = authority.RouteConsumption(entity, owner?.SessionId, context.Sender.SessionId, packet.Amount,
            out PowerSourceMetadata accepted, out string reason);
        diagnostics.RecordConsumption(packet.SourceId, context.Sender.SessionId, owner?.SessionId, packet.Amount, route, accepted, reason);
        switch (route)
        {
            case ConsumptionRoute.FORWARD_TO_OWNER:
                await context.SendAsync(packet, owner!.SessionId);
                break;
            case ConsumptionRoute.APPLIED_BY_SERVER:
                await context.SendToAllAsync(new BasePowerSourceUpdate(packet.SourceId, accepted.SourceType, accepted.Power, 0,
                    accepted.MaxPower, accepted.Revision, true, true, "", accepted.FuelConsumed));
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
