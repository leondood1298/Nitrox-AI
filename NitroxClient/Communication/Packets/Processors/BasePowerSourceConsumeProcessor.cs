using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;

namespace NitroxClient.Communication.Packets.Processors;

/// <summary>
///     Applies energy another player consumed from a power source this client simulates. The reduced level then
///     reaches everyone through the normal broadcast of that source.
/// </summary>
internal sealed class BasePowerSourceConsumeProcessor(SimulationOwnership simulationOwnership) : IClientPacketProcessor<BasePowerSourceConsume>
{
    public Task Process(ClientProcessorContext context, BasePowerSourceConsume packet)
    {
        // Ownership can move while the request travels. The new owner's level came from the server's stored state,
        // so applying here would not reach anyone; dropping a fraction of a second of draw is the smaller error.
        if (!simulationOwnership.HasAnyLockType(packet.SourceId))
        {
            return Task.CompletedTask;
        }
        if (!NitroxEntity.TryGetComponentFrom(packet.SourceId, out PowerSource powerSource))
        {
            Log.WarnOnce($"[BasePower] Could not find power source {packet.SourceId} to apply another player's consumption");
            return Task.CompletedTask;
        }

        // Negative amount consumes. The game clamps at zero and ignores it in modes without power requirements.
        powerSource.ModifyPower(-packet.Amount, out _);
        return Task.CompletedTask;
    }
}
