using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using UnityEngine;

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
///     <see cref="EnterConsumer" /> and <see cref="ExitConsumer" />. Consumers whose update runs on every client (base
///     spotlights, floodlights, water filtration) draw through <see cref="ConsumeEnergy" />, which lets only the
///     player simulating the consumer draw.
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
    ///     Decides whether this client draws for a consumer whose update runs on every client.
    ///     <para>
    ///     Letting every client draw and trusting the source's owner to be the one that counts does not work: a relay
    ///     drains its first source that has power, in an order that differs per client (PowerSource.Start connects
    ///     after a random delay), and a base's sources can have different owners. Each client could then drain a source
    ///     it owns (counted once per player) or one it does not own (never counted).
    ///     </para>
    /// </summary>
    public static SharedConsumerDraw GetSharedConsumerDraw(bool hasId, bool simulatedLocally)
    {
        if (!hasId)
        {
            return SharedConsumerDraw.LOCAL_ONLY;
        }
        return simulatedLocally ? SharedConsumerDraw.DRAW_AND_REPORT : SharedConsumerDraw.NONE;
    }

    /// <summary>
    ///     Stands in for PowerSystem.ConsumeEnergy in the update of a consumer that every client runs.
    /// </summary>
    /// <param name="consumer">The component carrying the consumer's Nitrox id.</param>
    public bool ConsumeEnergy(Component consumer, IPowerInterface powerInterface, float amount, out float amountConsumed)
    {
        NitroxId consumerId = null;
        bool hasId = consumer && consumer.TryGetNitroxId(out consumerId);
        switch (GetSharedConsumerDraw(hasId, hasId && simulationOwnership.HasAnyLockType(consumerId)))
        {
            case SharedConsumerDraw.DRAW_AND_REPORT:
                EnterConsumer();
                try
                {
                    return PowerSystem.ConsumeEnergy(powerInterface, amount, out amountConsumed);
                }
                finally
                {
                    ExitConsumer();
                }
            case SharedConsumerDraw.NONE:
                amountConsumed = 0f;
                return false;
            default:
                return PowerSystem.ConsumeEnergy(powerInterface, amount, out amountConsumed);
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

public enum SharedConsumerDraw
{
    /// <summary>
    ///     Another player simulates the consumer and draws for it.
    /// </summary>
    NONE,

    /// <summary>
    ///     This client simulates the consumer: it draws, and what comes from sources of other players is reported.
    /// </summary>
    DRAW_AND_REPORT,

    /// <summary>
    ///     The consumer is not a synchronized entity; it behaves as in the game.
    /// </summary>
    LOCAL_ONLY
}
