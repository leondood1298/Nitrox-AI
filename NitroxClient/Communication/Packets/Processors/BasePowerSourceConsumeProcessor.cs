using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;

namespace NitroxClient.Communication.Packets.Processors;

/// <summary>
///     Applies energy another player consumed from a power source this client simulates. The reduced level then
///     reaches everyone as the source's next state.
/// </summary>
internal sealed class BasePowerSourceConsumeProcessor(SimulationOwnership simulationOwnership, BasePowerSources basePowerSources) : IClientPacketProcessor<BasePowerSourceConsume>
{
    public Task Process(ClientProcessorContext context, BasePowerSourceConsume packet)
    {
        bool sourceObjectExists = NitroxEntity.TryGetComponentFrom(packet.SourceId, out PowerSource powerSource);
        // During a resync the source is only gone until its base has been rebuilt, like during the initial load
        bool finishedLoading = Multiplayer.Main && Multiplayer.Main.InitialSyncCompleted && !BasePowerSources.IsRebuildingBases();
        switch (basePowerSources.DecideForwardedDraw(packet.SourceId, simulationOwnership.HasAnyLockType(packet.SourceId), sourceObjectExists, finishedLoading))
        {
            case ForwardedDraw.APPLY:
                // Not ModifyPower: the game ignores that in modes without power requirements, and the player who took
                // this energy needs it accounted for even when a Creative player simulates the source. SetPower clamps
                // at zero.
                powerSource.SetPower(BasePowerSources.LevelAfterDraw(powerSource.power, packet.Amount));
                break;
            case ForwardedDraw.HAND_BACK:
                // A source that is missing here would stay frozen for everyone for as long as this client simulates it
                Log.Warn($"[BasePower] Power source {packet.SourceId} does not exist on this client; handing its simulation back");
                simulationOwnership.StopSimulatingEntity(packet.SourceId);
                context.Send(new SimulationOwnershipRelease(packet.SourceId));
                // Sent after the release, so the server passes it to whoever simulates the source from now on
                context.Send(packet);
                break;
            default:
                // Ownership can move while the request travels. Whoever simulates the source now started from the
                // level everyone had, so applying here would not reach anyone; dropping a fraction of a second of draw
                // is the smaller error.
                break;
        }
        return Task.CompletedTask;
    }
}
