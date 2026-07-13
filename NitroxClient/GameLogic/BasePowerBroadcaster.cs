using Nitrox.Model.DataStructures;
using System.Collections.Generic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.Communication.Abstract;
using NitroxClient.Extensions;
using UnityEngine;

namespace NitroxClient.GameLogic;

public static class BasePowerBroadcaster
{
	private const float BROADCAST_THROTTLE_SECONDS = 1f;
	private const float UNCHANGED_HEARTBEAT_SECONDS = 30f;
	private const float POWER_CHANGE_EPSILON = 0.001f;
	private static readonly Dictionary<NitroxId, float> lastBroadcastTimeBySource = [];
	private static readonly Dictionary<NitroxId, float> lastBroadcastPowerBySource = [];
	private static readonly Dictionary<NitroxId, float> lastBroadcastFuelConsumedBySource = [];

	public static void BroadcastIfOwner(Component owner, PowerSource powerSource, SimulationOwnership simulationOwnership, BasePowerState state, IPacketSender packetSender)
	{
		if (!(bool)powerSource || !owner.TryGetNitroxId(out NitroxId nitroxId) || !simulationOwnership.HasAnyLockType(nitroxId))
		{
			return;
		}
		float now = Time.realtimeSinceStartup;
		bool hasPreviousTime = lastBroadcastTimeBySource.TryGetValue(nitroxId, out float lastBroadcast);
		float elapsed = hasPreviousTime ? now - lastBroadcast : float.PositiveInfinity;
		if (elapsed < BROADCAST_THROTTLE_SECONDS)
		{
			return;
		}
		float fuelConsumed = BasePowerSources.GetFuelConsumed(owner);
		bool powerUnchanged = lastBroadcastPowerBySource.TryGetValue(nitroxId, out float previousPower) && Mathf.Abs(previousPower - powerSource.power) < POWER_CHANGE_EPSILON;
		bool fuelUnchanged = lastBroadcastFuelConsumedBySource.TryGetValue(nitroxId, out float previousFuelConsumed) && Mathf.Abs(previousFuelConsumed - fuelConsumed) < POWER_CHANGE_EPSILON;
		bool unchanged = powerUnchanged && fuelUnchanged;
		if (unchanged && elapsed < UNCHANGED_HEARTBEAT_SECONDS)
		{
			return;
		}
		BasePowerSourceType sourceType = BasePowerSources.GetSourceType(owner);
		if (sourceType == BasePowerSourceType.UNKNOWN)
		{
			return;
		}
		lastBroadcastTimeBySource[nitroxId] = now;
		lastBroadcastPowerBySource[nitroxId] = powerSource.power;
		lastBroadcastFuelConsumedBySource[nitroxId] = fuelConsumed;
		packetSender.Send(state.CreateUpdate(nitroxId, sourceType, powerSource.power, fuelConsumed));
	}
}


