using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;

namespace NitroxClient.GameLogic;

/// <summary>
///     Reports energy this client consumed from base power sources that another player simulates.
///     <para>
///     A power source's level is owned by the player simulating it: that player generates, and broadcasts the level to
///     everyone else. Things that draw power (docked vehicles, chargers, Scanner Rooms, fabricators) run on the player
///     simulating the consumer instead. When those are different players the draw only changed the consumer's local
///     copy and was overwritten by the owner's next broadcast, so the base never lost power.
///     </para>
///     <para>
///     Only consumers that run on exactly one client report here, by wrapping their update in
///     <see cref="EnterConsumer" /> and <see cref="ExitConsumer" />. Consumers that run on every client (base
///     spotlights, floodlights, water filtration) must not: the source owner runs them too and already counts them
///     once, so reporting would count them once per player.
///     </para>
/// </summary>
public sealed class BasePowerConsumptionForwarder(IPacketSender packetSender, SimulationOwnership simulationOwnership)
{
    private readonly BasePowerConsumptionLedger ledger = new();
    private int consumerDepth;

    /// <summary>
    ///     True while a single-client consumer is drawing power.
    /// </summary>
    public bool IsConsumerActive => consumerDepth > 0;

    public void EnterConsumer() => consumerDepth++;

    public void ExitConsumer()
    {
        if (consumerDepth > 0)
        {
            consumerDepth--;
        }
    }

    /// <summary>
    ///     Called when a power source's level dropped locally by <paramref name="consumed" />.
    /// </summary>
    public void RecordConsumption(NitroxId sourceId, float consumed)
    {
        // Our own simulation of this source already includes the draw and will broadcast it.
        if (!IsConsumerActive || simulationOwnership.HasAnyLockType(sourceId))
        {
            return;
        }
        ledger.Record(sourceId, consumed);
    }

    /// <summary>
    ///     Sends what has accumulated. Call once per frame.
    /// </summary>
    public void Flush(float now)
    {
        foreach (KeyValuePair<NitroxId, float> due in ledger.TakeDue(now))
        {
            packetSender.Send(new BasePowerSourceConsume(due.Key, due.Value));
        }
    }
}
