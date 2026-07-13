using System;
using System.Collections.Generic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using UnityEngine;

namespace NitroxClient.GameLogic;

public static class ReactorFuelDisplay
{
	public static string GetStatus(BaseBioReactor reactor)
	{
		float fuelCapacity = 0f;
		int itemCount = 0;
		if (reactor.container != null)
		{
			foreach (InventoryItem inventoryItem in (IEnumerable<InventoryItem>)reactor.container)
			{
				if (inventoryItem?.item == null)
				{
					continue;
				}
				float charge = BaseBioReactor.GetCharge(inventoryItem.item.GetTechType());
				if (charge > 0f)
				{
					fuelCapacity += charge;
					itemCount++;
				}
			}
		}
		return FormatStatus(itemCount, "Nitrox_ReactorFuelItems", fuelCapacity, reactor._toConsume, BaseBioReactor.powerPerSecond);
	}

	public static string GetStatus(BaseNuclearReactor reactor)
	{
		int rodCount = reactor.CountActiveRods();
		float fuelCapacity = rodCount * BasePowerSourceTypes.NUCLEAR_MAX_FUEL_PROGRESS;
		return FormatStatus(rodCount, "Nitrox_ReactorFuelRods", fuelCapacity, reactor._toConsume, BaseNuclearReactor.powerPerSecond);
	}

	public static float CalculateRemainingEnergy(float fuelCapacity, float fuelConsumed) => Math.Max(0f, fuelCapacity - fuelConsumed);

	public static string FormatRuntime(float seconds)
	{
		int totalMinutes = Math.Max(1, Mathf.CeilToInt(seconds / 60f));
		int hours = totalMinutes / 60;
		int minutes = totalMinutes % 60;
		if (hours == 0)
		{
			return $"{minutes}m";
		}
		return minutes == 0 ? $"{hours}h" : $"{hours}h {minutes}m";
	}

	private static string FormatStatus(int itemCount, string itemLabelKey, float fuelCapacity, float fuelConsumed, float powerPerSecond)
	{
		float remaining = CalculateRemainingEnergy(fuelCapacity, fuelConsumed);
		if (itemCount == 0 || remaining <= 0f)
		{
			return Language.main.Get("Nitrox_ReactorFuelEmpty");
		}
		string runtime = FormatRuntime(remaining / Math.Max(powerPerSecond, float.Epsilon));
		return Language.main.Get("Nitrox_ReactorFuelStatus")
		                    .Replace("{COUNT}", itemCount.ToString())
		                    .Replace("{ITEMS}", Language.main.Get(itemLabelKey))
		                    .Replace("{ENERGY}", Mathf.CeilToInt(remaining).ToString())
		                    .Replace("{TIME}", runtime);
	}
}
