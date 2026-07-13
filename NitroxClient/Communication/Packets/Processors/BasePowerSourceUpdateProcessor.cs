using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class BasePowerSourceUpdateProcessor(BasePowerState state) : IClientPacketProcessor<BasePowerSourceUpdate>
{
    public Task Process(ClientProcessorContext context, BasePowerSourceUpdate packet)
    {
        if (!packet.IsServerResponse)
        {
            return Task.CompletedTask;
        }
        PowerSourceMetadata requested = new(packet.Power, packet.MaxPower, packet.SourceType, packet.Revision);
        if (!state.TryApply(packet.SourceId, requested, out PowerSourceMetadata accepted))
        {
            return Task.CompletedTask;
        }
        if (!packet.Granted)
        {
            Log.Warn($"[BasePower] Server rejected source {packet.SourceId} sequence {packet.ClientSequence}: {packet.RejectionReason}. Restoring revision {packet.Revision}.");
        }
        if (NitroxEntity.TryGetComponentFrom(packet.SourceId, out PowerSource powerSource))
        {
            powerSource.SetPower(accepted.Power);
        }
        return Task.CompletedTask;
    }
}
