using System;
using System.Collections.Generic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     The line about a reactor's fuel that is shown below the game's own hand text, which only has the stored power:
///     how many fuel items the reactor holds, the energy left in them, and how long that lasts at full output.
/// </summary>
public static class ReactorFuelDisplay
{
    private const float SECONDS_PER_MINUTE = 60f;
    private const int MINUTES_PER_HOUR = 60;

    /// <summary>
    ///     Called after the game set a bioreactor's hand text (BaseBioReactor.OnHover). Sets it again, with the fuel
    ///     line below the game's own.
    /// </summary>
    public static void ShowHandText(BaseBioReactor reactor) => ShowHandText("UseBaseBioReactor", reactor._powerSource, GetStatus(reactor));

    /// <inheritdoc cref="ShowHandText(BaseBioReactor)" />
    public static void ShowHandText(BaseNuclearReactor reactor) => ShowHandText("UseBaseNuclearReactor", reactor._powerSource, GetStatus(reactor));

    public static string GetStatus(BaseBioReactor reactor)
    {
        float fuelCapacity = 0f;
        int itemCount = 0;
        if (reactor.container != null)
        {
            foreach (InventoryItem inventoryItem in (IEnumerable<InventoryItem>)reactor.container)
            {
                if (inventoryItem == null || !inventoryItem.item)
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
        int totalMinutes = Math.Max(1, Mathf.CeilToInt(seconds / SECONDS_PER_MINUTE));
        int hours = totalMinutes / MINUTES_PER_HOUR;
        int minutes = totalMinutes % MINUTES_PER_HOUR;
        if (hours == 0)
        {
            return $"{minutes}m";
        }
        return minutes == 0 ? $"{hours}h" : $"{hours}h {minutes}m";
    }

    /// <param name="gameTextKey">The language key of the game's own line, which takes the stored power and the capacity.</param>
    private static void ShowHandText(string gameTextKey, PowerSource powerSource, string fuelStatus)
    {
        string gameText = Language.main.GetFormat(gameTextKey, Mathf.RoundToInt(powerSource.GetPower()), Mathf.RoundToInt(powerSource.GetMaxPower()));
        HandReticle.main.SetText(HandReticle.TextType.Hand, $"{gameText}\n{fuelStatus}", false, GameInput.Button.LeftHand);
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
