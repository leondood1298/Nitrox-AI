using System;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using UnityEngine;

namespace NitroxClient.GameLogic;

public static class BasePowerSources
{
    /// <summary>
    ///     Whether an addition to a source's level has to be applied although the game would ignore it.
    ///     <p />
    ///     PowerSource.ModifyPower leaves the level alone when the local player's game mode needs no power (Creative).
    ///     A source's level is shared: when a Creative player simulates it, the players who do need power still depend
    ///     on what it generates. Draws the Creative player's own game makes are left to the game, which makes them free.
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
    ///     <p />
    ///     Not while it rebuilds its bases from a resync: a reactor is then back before its fuel is. A nuclear reactor
    ///     without rods zeroes its fuel progress and takes that progress off its stored power
    ///     (BaseNuclearReactor.ProducePower), and both would be broadcast as the source's state before the resync has
    ///     put the real values back.
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
    ///     The level after another player took <paramref name="amount" /> from a source this client simulates.
    /// </summary>
    public static float LevelAfterDraw(float power, float amount) => Math.Max(0f, power - amount);

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
}
