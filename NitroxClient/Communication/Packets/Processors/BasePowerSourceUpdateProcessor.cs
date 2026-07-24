using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class BasePowerSourceUpdateProcessor(BasePowerState state, BasePowerClientDiagnostics diagnostics) : IClientPacketProcessor<BasePowerSourceUpdate>
{
    public Task Process(ClientProcessorContext context, BasePowerSourceUpdate packet)
    {
        if (!packet.IsServerResponse)
        {
            return Task.CompletedTask;
        }
        PowerSourceMetadata requested = new(packet.Power, packet.MaxPower, packet.SourceType, packet.Revision, packet.FuelConsumed);
        if (!state.TryApply(packet.SourceId, requested, out PowerSourceMetadata accepted))
        {
            return Task.CompletedTask;
        }
        if (!packet.Granted)
        {
            Log.Warn($"[BasePower] Server rejected source {packet.SourceId} sequence {packet.ClientSequence}: {packet.RejectionReason}. Restoring revision {packet.Revision}.");
        }
        bool objectFound = NitroxEntity.TryGetComponentFrom(packet.SourceId, out PowerSource powerSource);
        if (objectFound)
        {
            powerSource.SetPower(accepted.Power);
            BasePowerSources.SetFuelConsumed(powerSource, accepted.SourceType, accepted.FuelConsumed);
        }
        RecordReconciliationApply(packet.SourceId, accepted, objectFound, "packet");
        return Task.CompletedTask;
    }

    private void RecordReconciliationApply(Nitrox.Model.DataStructures.NitroxId sourceId, PowerSourceMetadata metadata,
        bool objectFound, string reason)
    {
        bool initialSyncCompleted = Multiplayer.Main && Multiplayer.Main.InitialSyncCompleted;
        bool waitScreenWaiting = WaitScreen.IsWaiting;
        if (Multiplayer.Active && (!initialSyncCompleted || waitScreenWaiting))
        {
            diagnostics.RecordSourceApply(sourceId, metadata, objectFound, initialSyncCompleted, waitScreenWaiting, reason);
        }
    }
}
