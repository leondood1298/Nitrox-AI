using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic.Bases;
using UnityEngine;

namespace NitroxClient.GameLogic;

public static class BasePowerBroadcaster
{
    private const float BROADCAST_THROTTLE_SECONDS = 1f;
    private const float UNCHANGED_HEARTBEAT_SECONDS = 30f;

    /// <summary>
    ///     Share of a source's capacity its level or fuel progress has to move before the new state is worth a packet.
    ///     A source that is charging changes every frame; sending each second of that for every source adds up to one
    ///     packet per source per second for every player, for a difference nobody can see.
    /// </summary>
    private const float CHANGE_FRACTION_OF_CAPACITY = 0.01f;

    private const float MIN_CHANGE = 0.001f;

    /// <summary>
    ///     True while this client replaces its bases with the server's copy (see <see cref="BasePowerSources.CanSimulate" />).
    /// </summary>
    public static bool IsRebuildingBases() => BuildingHandler.Main && BuildingHandler.Main.Resyncing;

    /// <summary>
    ///     Decides whether a source's state is sent now.
    /// </summary>
    /// <param name="last">What was sent last for this source, or null when nothing was.</param>
    public static bool ShouldBroadcast(BasePowerState.Broadcast? last, float now, float power, float maxPower, float fuelConsumed)
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

    public static void BroadcastIfOwner(Component owner, PowerSource powerSource, SimulationOwnership simulationOwnership, BasePowerState state, IPacketSender packetSender)
    {
        if (!powerSource || !owner.TryGetNitroxId(out NitroxId nitroxId) || !BasePowerSources.CanSimulate(true, simulationOwnership.HasAnyLockType(nitroxId), IsRebuildingBases()))
        {
            return;
        }
        float now = Time.realtimeSinceStartup;
        float fuelConsumed = BasePowerSources.GetFuelConsumed(owner);
        if (!ShouldBroadcast(state.GetLastBroadcast(nitroxId), now, powerSource.power, powerSource.maxPower, fuelConsumed))
        {
            return;
        }
        BasePowerSourceType sourceType = BasePowerSources.GetSourceType(owner);
        if (sourceType == BasePowerSourceType.UNKNOWN)
        {
            return;
        }
        state.RecordBroadcast(nitroxId, new BasePowerState.Broadcast(now, powerSource.power, fuelConsumed));
        packetSender.Send(state.CreateUpdate(nitroxId, sourceType, powerSource.power, fuelConsumed));
    }
}
