namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     What a base power source of each kind can hold. The server checks every reported state against these, because
///     it has no game to ask.
/// </summary>
public static class BasePowerSourceTypes
{
    // Taken to be the capacity (PowerSource.maxPower) of the game's solar panel, thermal plant, bioreactor and
    // nuclear reactor. It is data of the game's prefabs, which its IL does not show, and is not confirmed there.
    private const float SOLAR_MAX_POWER = 75f;
    private const float THERMAL_MAX_POWER = 250f;
    private const float BIOREACTOR_MAX_POWER = 500f;
    public const float NUCLEAR_MAX_POWER = 2500f;

    /// <summary>
    ///     A reactor's fuel progress (the game's _toConsume) is what it has burned of the item it is burning, so it
    ///     stays below the energy of the richest item: the largest entry of BaseBioReactor.charge, read from the IL
    ///     of that class's static constructor.
    /// </summary>
    public const float BIOREACTOR_MAX_FUEL_PROGRESS = 840f;

    /// <summary>
    ///     The energy of a reactor rod, the only entry of BaseNuclearReactor.charge (read from the IL as well).
    /// </summary>
    public const float NUCLEAR_MAX_FUEL_PROGRESS = 20000f;

    public static bool TryGetMaxPower(BasePowerSourceType sourceType, out float maxPower)
    {
        maxPower = sourceType switch
        {
            BasePowerSourceType.SOLAR => SOLAR_MAX_POWER,
            BasePowerSourceType.THERMAL => THERMAL_MAX_POWER,
            BasePowerSourceType.BIOREACTOR => BIOREACTOR_MAX_POWER,
            BasePowerSourceType.NUCLEAR => NUCLEAR_MAX_POWER,
            _ => 0f
        };
        return maxPower > 0f;
    }

    public static bool TryGetMaxFuelProgress(BasePowerSourceType sourceType, out float maxFuelProgress)
    {
        maxFuelProgress = sourceType switch
        {
            BasePowerSourceType.BIOREACTOR => BIOREACTOR_MAX_FUEL_PROGRESS,
            BasePowerSourceType.NUCLEAR => NUCLEAR_MAX_FUEL_PROGRESS,
            _ => 0f
        };
        return maxFuelProgress > 0f;
    }
}
