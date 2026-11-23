using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic.Bases;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     The base power sources (solar panel, thermal plant, bioreactor, nuclear reactor) as this client sees them. The
///     player simulating a source generates its power and tells everyone its level as entity metadata; this holds the
///     rules for that and what the client has to remember per source in between.
/// </summary>
public sealed class BasePowerSources(IPacketSender packetSender, SimulationOwnership simulationOwnership)
{
    private const float BROADCAST_THROTTLE_SECONDS = 1f;

    /// <summary>
    ///     A source whose level does not move is still sent this often. The copy another player holds can drift from
    ///     what that player's own game does to it, and nothing else would put it right while the level is pinned.
    /// </summary>
    private const float UNCHANGED_HEARTBEAT_SECONDS = 30f;

    /// <summary>
    ///     Share of a source's capacity its level or fuel progress has to move before the new state is worth a packet.
    ///     A source that is charging changes every frame; sending each second of that for every source adds up to one
    ///     packet per source per second for every player, for a difference nobody can see.
    /// </summary>
    private const float CHANGE_FRACTION_OF_CAPACITY = 0.01f;

    private const float MIN_CHANGE = 0.001f;

    private readonly Dictionary<NitroxId, Broadcast> lastBroadcastBySource = [];
    private readonly HashSet<NitroxId> handedBackSources = [];

    /// <summary>
    ///     What this client last sent for a source it simulates.
    /// </summary>
    /// <param name="Time">Time.realtimeSinceStartup when it was sent.</param>
    public readonly record struct Broadcast(float Time, float Power, float FuelConsumed);

    /// <summary>
    ///     True while this client replaces its bases with the server's copy (see <see cref="CanSimulate" />).
    /// </summary>
    public static bool IsRebuildingBases() => BuildingHandler.Main && BuildingHandler.Main.Resyncing;

    /// <summary>
    ///     Whether an addition to a source's level has to be applied although the game would ignore it.
    ///     <para>
    ///     PowerSource.ModifyPower leaves the level alone when the local player's game mode needs no power (Creative).
    ///     A source's level is shared: when a Creative player simulates it, the players who do need power still depend
    ///     on what it generates. Draws the Creative player's own game makes are left to the game, which makes them free.
    ///     </para>
    /// </summary>
    public static bool MustGenerateWithoutPowerRequirement(bool localModeRequiresPower, bool simulatedLocally, float amount) =>
        !localModeRequiresPower && simulatedLocally && amount > 0f;

    /// <summary>
    ///     The level after adding <paramref name="amount" />, as PowerSource.ModifyPower computes it in a mode that
    ///     requires power.
    /// </summary>
    public static float LevelAfterGenerating(float power, float maxPower, float amount) => Math.Max(0f, Math.Min(power + amount, maxPower));

    /// <summary>
    ///     Whether this client runs a source's simulation right now.
    ///     <para>
    ///     Not while it rebuilds its bases from a resync: a reactor is then back before its fuel is. A nuclear reactor
    ///     without rods zeroes its fuel progress and takes that progress off its stored power
    ///     (BaseNuclearReactor.ProducePower), and both would be broadcast as the source's state before the resync has
    ///     put the real values back.
    ///     </para>
    /// </summary>
    public static bool CanSimulate(bool hasId, bool simulatedLocally, bool rebuildingBases)
    {
        if (!hasId)
        {
            // Not a synchronized entity: it behaves as in the game
            return true;
        }
        return simulatedLocally && !rebuildingBases;
    }

    /// <summary>
    ///     Whether a state that arrived for a source is the server's answer to a state it refused from this client.
    ///     <para>
    ///     The player simulating a source is not sent that source's state otherwise: what the server accepts goes to
    ///     the other players, and the server only changes the level itself while nobody simulates the source. While
    ///     loading or rebuilding bases the state that arrives is the one the source spawns with.
    ///     </para>
    /// </summary>
    public static bool IsAnswerToRefusedState(bool simulatedLocally, bool finishedLoading) => simulatedLocally && finishedLoading;

    /// <summary>
    ///     The level after another player took <paramref name="amount" /> from a source this client simulates.
    /// </summary>
    public static float LevelAfterDraw(float power, float amount) => Math.Max(0f, power - amount);

    /// <summary>
    ///     Decides whether a source's state is sent now.
    /// </summary>
    /// <param name="last">What was sent last for this source, or null when nothing was.</param>
    public static bool ShouldBroadcast(Broadcast? last, float now, float power, float maxPower, float fuelConsumed)
    {
        if (last is not { } previous)
        {
            return true;
        }
        float elapsed = now - previous.Time;
        if (elapsed < BROADCAST_THROTTLE_SECONDS)
        {
            return false;
        }
        if (elapsed >= UNCHANGED_HEARTBEAT_SECONDS)
        {
            return true;
        }
        // Running empty and filling up decide whether the base has power and whether the source still generates, so
        // those are never held back
        if ((previous.Power <= 0f) != (power <= 0f) || (previous.Power >= maxPower) != (power >= maxPower))
        {
            return true;
        }
        float worthSending = Math.Max(maxPower * CHANGE_FRACTION_OF_CAPACITY, MIN_CHANGE);
        return Math.Abs(power - previous.Power) >= worthSending || Math.Abs(fuelConsumed - previous.FuelConsumed) >= worthSending;
    }

    public static BasePowerSourceType GetSourceType(Component component)
    {
        if (component is SolarPanel || component.GetComponentInParent<SolarPanel>())
        {
            return BasePowerSourceType.SOLAR;
        }
        if (component is ThermalPlant || component.GetComponentInParent<ThermalPlant>())
        {
            return BasePowerSourceType.THERMAL;
        }
        if (component is BaseBioReactor || component.GetComponentInParent<BaseBioReactor>())
        {
            return BasePowerSourceType.BIOREACTOR;
        }
        if (component is BaseNuclearReactor || component.GetComponentInParent<BaseNuclearReactor>())
        {
            return BasePowerSourceType.NUCLEAR;
        }
        return BasePowerSourceType.UNKNOWN;
    }

    public static float GetFuelConsumed(Component component)
    {
        if (component is BaseBioReactor bioReactor || (bioReactor = component.GetComponentInParent<BaseBioReactor>()))
        {
            return bioReactor._toConsume;
        }
        if (component is BaseNuclearReactor nuclearReactor || (nuclearReactor = component.GetComponentInParent<BaseNuclearReactor>()))
        {
            return nuclearReactor._toConsume;
        }
        return 0f;
    }

    public static void SetFuelConsumed(Component component, BasePowerSourceType sourceType, float fuelConsumed)
    {
        BasePowerSourceTypes.TryGetMaxFuelProgress(sourceType, out float maximum);
        float accepted = Mathf.Clamp(fuelConsumed, 0f, maximum);
        if (sourceType == BasePowerSourceType.BIOREACTOR && (component is BaseBioReactor bioReactor || (bioReactor = component.GetComponentInParent<BaseBioReactor>())))
        {
            bioReactor._toConsume = accepted;
        }
        else if (sourceType == BasePowerSourceType.NUCLEAR && (component is BaseNuclearReactor nuclearReactor || (nuclearReactor = component.GetComponentInParent<BaseNuclearReactor>())))
        {
            nuclearReactor._toConsume = accepted;
        }
    }

    /// <summary>
    ///     The temperature a thermal plant has reached, zero for anything else.
    /// </summary>
    public static float GetTemperature(Component component)
    {
        ThermalPlant thermalPlant = component as ThermalPlant;
        if (!thermalPlant)
        {
            thermalPlant = component.GetComponentInParent<ThermalPlant>();
        }
        return thermalPlant ? thermalPlant.temperature : 0f;
    }

    /// <summary>
    ///     Tells everyone the state of a source this client simulates, when it moved enough to matter.
    /// </summary>
    /// <param name="owner">The solar panel, thermal plant or reactor component, which carries the Nitrox id.</param>
    public void BroadcastIfSimulated(Component owner, PowerSource source)
    {
        if (!source || !owner.TryGetNitroxId(out NitroxId sourceId) || !CanSimulate(true, simulationOwnership.HasAnyLockType(sourceId), IsRebuildingBases()))
        {
            return;
        }
        PowerSourceMetadata state = new(source.power, GetFuelConsumed(owner), GetSourceType(owner), GetTemperature(owner));
        if (TryCreateUpdate(sourceId, state, source.maxPower, Time.realtimeSinceStartup, out EntityMetadataUpdate update))
        {
            packetSender.Send(update);
        }
    }

    /// <summary>
    ///     Returns the update to send for a simulated source, and remembers it as sent, when <see cref="ShouldBroadcast" />
    ///     says its state is due.
    /// </summary>
    internal bool TryCreateUpdate(NitroxId sourceId, PowerSourceMetadata state, float maxPower, float now, out EntityMetadataUpdate update)
    {
        update = null;
        if (state.SourceType == BasePowerSourceType.UNKNOWN)
        {
            return false;
        }
        Broadcast? last = lastBroadcastBySource.TryGetValue(sourceId, out Broadcast previous) ? previous : null;
        if (!ShouldBroadcast(last, now, state.Power, maxPower, state.FuelConsumed))
        {
            return false;
        }
        lastBroadcastBySource[sourceId] = new Broadcast(now, state.Power, state.FuelConsumed);
        update = new EntityMetadataUpdate(sourceId, state);
        return true;
    }

    /// <summary>
    ///     Decides what to do with energy another player took from a source the server says this client simulates. A
    ///     hand-back is decided at most once per source.
    /// </summary>
    /// <param name="simulatedLocally">False when the simulation moved on while the request travelled.</param>
    /// <param name="finishedLoading">While loading, a missing object is still about to spawn.</param>
    public ForwardedDraw DecideForwardedDraw(NitroxId sourceId, bool simulatedLocally, bool sourceObjectExists, bool finishedLoading)
    {
        if (!simulatedLocally)
        {
            return ForwardedDraw.IGNORE;
        }
        if (sourceObjectExists)
        {
            return ForwardedDraw.APPLY;
        }
        // Only once per source: if it comes back because nobody else can simulate it either, handing it back again
        // would only pass it around.
        if (!finishedLoading || !handedBackSources.Add(sourceId))
        {
            return ForwardedDraw.IGNORE;
        }
        return ForwardedDraw.HAND_BACK;
    }

    /// <summary>
    ///     Drops what is remembered about a source that no longer exists, so that it cannot hold back the first state of
    ///     a later entity or stop that entity from being handed back.
    /// </summary>
    public void Forget(NitroxId sourceId)
    {
        lastBroadcastBySource.Remove(sourceId);
        handedBackSources.Remove(sourceId);
    }
}

public enum ForwardedDraw
{
    /// <summary>
    ///     Nothing to do here: someone else simulates the source by now, or this client cannot do better.
    /// </summary>
    IGNORE,

    /// <summary>
    ///     Take the energy from the source.
    /// </summary>
    APPLY,

    /// <summary>
    ///     The source does not exist on this client: give its simulation back so that someone who has it takes over.
    /// </summary>
    HAND_BACK
}
