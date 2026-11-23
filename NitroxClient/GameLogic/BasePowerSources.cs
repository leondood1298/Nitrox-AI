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
    /// <summary>
    ///     The shortest time between two states of one source. A guess, not measured.
    /// </summary>
    private const float BROADCAST_THROTTLE_SECONDS = 1f;

    /// <summary>
    ///     A source whose level does not move is still sent this often. The copy another player holds can drift from
    ///     what that player's own game does to it, and nothing else would put it right while the level is pinned.
    ///     The number is a guess. The server's basepower command counts on it: it calls a source that was not
    ///     reported for three times as long frozen.
    /// </summary>
    private const float UNCHANGED_HEARTBEAT_SECONDS = 30f;

    /// <summary>
    ///     Share of a source's capacity its level or fuel progress has to move before the new state is worth a packet.
    ///     A source that is charging changes every frame; sending each second of that for every source adds up to one
    ///     packet per source per second for every player, for a difference nobody can see. One percent is a guess.
    /// </summary>
    private const float CHANGE_FRACTION_OF_CAPACITY = 0.01f;

    /// <summary>
    ///     What a change has to reach at least, whatever the capacity is said to be. A guess at more than float noise.
    /// </summary>
    private const float MIN_CHANGE = 0.001f;

    private readonly Dictionary<NitroxId, Broadcast> lastBroadcastBySource = [];
    private readonly HashSet<NitroxId> handedBackSources = [];

    /// <summary>
    ///     What this client last sent for a source it simulates.
    /// </summary>
    /// <param name="Time">Time.realtimeSinceStartup when it was sent.</param>
    public readonly record struct Broadcast(float Time, float Power, float FuelConsumed);

    /// <summary>
    ///     True while this client replaces its bases with the server's copy (see <see cref="CanSimulate(bool, bool, bool)" />).
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
        if (TryGetOwner(component, out SolarPanel _))
        {
            return BasePowerSourceType.SOLAR;
        }
        if (TryGetOwner(component, out ThermalPlant _))
        {
            return BasePowerSourceType.THERMAL;
        }
        if (TryGetOwner(component, out BaseBioReactor _))
        {
            return BasePowerSourceType.BIOREACTOR;
        }
        if (TryGetOwner(component, out BaseNuclearReactor _))
        {
            return BasePowerSourceType.NUCLEAR;
        }
        return BasePowerSourceType.UNKNOWN;
    }

    public static float GetFuelConsumed(Component component)
    {
        if (TryGetOwner(component, out BaseBioReactor bioReactor))
        {
            return bioReactor._toConsume;
        }
        if (TryGetOwner(component, out BaseNuclearReactor nuclearReactor))
        {
            return nuclearReactor._toConsume;
        }
        return 0f;
    }

    public static void SetFuelConsumed(Component component, BasePowerSourceType sourceType, float fuelConsumed)
    {
        BasePowerSourceTypes.TryGetMaxFuelProgress(sourceType, out float maximum);
        float accepted = Mathf.Clamp(fuelConsumed, 0f, maximum);
        if (sourceType == BasePowerSourceType.BIOREACTOR && TryGetOwner(component, out BaseBioReactor bioReactor))
        {
            bioReactor._toConsume = accepted;
        }
        else if (sourceType == BasePowerSourceType.NUCLEAR && TryGetOwner(component, out BaseNuclearReactor nuclearReactor))
        {
            nuclearReactor._toConsume = accepted;
        }
    }

    /// <summary>
    ///     The temperature a thermal plant has reached, zero for anything else.
    /// </summary>
    public static float GetTemperature(Component component) => TryGetOwner(component, out ThermalPlant thermalPlant) ? thermalPlant.temperature : 0f;

    /// <summary>
    ///     Finds the solar panel, thermal plant or reactor that <paramref name="component" /> is, or that its power
    ///     source belongs to.
    ///     <para>
    ///     Inactive objects are included: a base is put together while inactive when a player joins, and the stored
    ///     state is applied to its reactors then. A search that skips inactive objects finds no reactor at that point,
    ///     and the fuel progress of a part-used rod or bioreactor load would be dropped.
    ///     </para>
    /// </summary>
    private static bool TryGetOwner<T>(Component component, out T owner) where T : Component
    {
        owner = component as T;
        return owner || component.TryGetComponentInParent(out owner, true);
    }

    /// <summary>
    ///     Whether a source generates its power in this client's game: in the game of the player who simulates it, and
    ///     in everybody's if it is no synchronized entity. Reactors need the stricter <see cref="CanSimulate(Component)" />.
    /// </summary>
    /// <param name="owner">The solar panel or thermal plant component, which carries the Nitrox id.</param>
    public bool GeneratesHere(Component owner) => !owner.TryGetNitroxId(out NitroxId sourceId) || simulationOwnership.HasAnyLockType(sourceId);

    /// <inheritdoc cref="CanSimulate(bool, bool, bool)" />
    /// <param name="owner">The solar panel, thermal plant or reactor component, which carries the Nitrox id.</param>
    public bool CanSimulate(Component owner)
    {
        bool hasId = owner.TryGetNitroxId(out NitroxId sourceId);
        return CanSimulate(hasId, hasId && simulationOwnership.HasAnyLockType(sourceId), IsRebuildingBases());
    }

    /// <summary>
    ///     Stands in for PowerSource.ModifyPower where the game would ignore an addition that has to be applied (see
    ///     <see cref="MustGenerateWithoutPowerRequirement" />).
    /// </summary>
    /// <param name="consumed">Set to what the game's method gives out.</param>
    /// <param name="result">Set to what the game's method returns.</param>
    /// <returns>False if the game's own method has to run.</returns>
    public bool TryGenerateWithoutPowerRequirement(PowerSource source, float amount, ref float consumed, ref bool result)
    {
        // Checked first: nearly every player's mode requires power, and then the game does all of this itself
        if (GameModeUtils.RequiresPower())
        {
            return false;
        }
        bool simulatedLocally = amount > 0f && source.TryGetNitroxId(out NitroxId sourceId) && simulationOwnership.HasAnyLockType(sourceId);
        if (!MustGenerateWithoutPowerRequirement(false, simulatedLocally, amount))
        {
            return false;
        }

        // What the game returns in a mode without power requirements, so that its callers behave as they do there
        result = amount <= source.maxPower - source.power;
        consumed = amount;
        source.power = LevelAfterGenerating(source.power, source.maxPower, amount);
        return true;
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
