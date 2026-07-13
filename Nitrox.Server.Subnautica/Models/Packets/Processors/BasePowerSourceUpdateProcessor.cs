using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class BasePowerSourceUpdateProcessor(EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData,
    BasePowerSourceAuthority authority, BasePowerDiagnostics diagnostics, ILogger<BasePowerSourceUpdateProcessor> logger) : IAuthPacketProcessor<BasePowerSourceUpdate>
{
    public async Task Process(AuthProcessorContext context, BasePowerSourceUpdate packet)
    {
        if (!entityRegistry.TryGetEntityById(packet.SourceId, out Entity entity))
        {
            await Reject(context, packet, new PowerSourceMetadata(0f), "unknown source entity");
            return;
        }
        if (simulationOwnershipData.GetPlayerForLock(packet.SourceId) != context.Sender)
        {
            await Reject(context, packet, CanonicalOrFallback(entity, packet), "sender does not own source simulation");
            return;
        }
        if (!authority.TryApply(entity, context.Sender.SessionId, packet, out PowerSourceMetadata accepted, out string reason))
        {
            await Reject(context, packet, accepted, reason);
            return;
        }

        diagnostics.RecordAccepted();
        if (diagnostics.TraceEnabled)
        {
            logger.ZLogInformation($"[BasePower] accepted {accepted.SourceType} source {packet.SourceId}: {accepted.Power:F2}/{accepted.MaxPower:F2}, revision {accepted.Revision}, client sequence {packet.ClientSequence}, owner {context.Sender.Name} #{context.Sender.SessionId}");
        }
        await context.SendToAllAsync(Response(packet, accepted, true, ""));
    }

    private async Task Reject(AuthProcessorContext context, BasePowerSourceUpdate packet, PowerSourceMetadata canonical, string reason)
    {
        diagnostics.RecordRejected();
        logger.ZLogWarning($"[BasePower] rejected source {packet.SourceId} update from {context.Sender.Name} #{context.Sender.SessionId}: {reason}");
        await context.ReplyAsync(Response(packet, canonical, false, reason));
    }

    private static BasePowerSourceUpdate Response(BasePowerSourceUpdate request, PowerSourceMetadata canonical, bool granted, string reason) =>
        new(request.SourceId, canonical.SourceType, canonical.Power, request.ClientSequence, canonical.MaxPower, canonical.Revision, true, granted, reason);

    private static PowerSourceMetadata CanonicalOrFallback(Entity entity, BasePowerSourceUpdate packet)
    {
        if (entity.Metadata is PowerSourceMetadata metadata)
        {
            return metadata;
        }
        BasePowerSourceTypes.TryGetMaxPower(packet.SourceType, out float maxPower);
        float power = float.IsFinite(packet.Power) ? Math.Clamp(packet.Power, 0f, maxPower) : 0f;
        return new PowerSourceMetadata(power, maxPower, packet.SourceType, 0);
    }
}
