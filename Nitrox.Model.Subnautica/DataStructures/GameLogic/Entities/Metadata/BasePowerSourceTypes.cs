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
    ///     Not the capacity of a Lifepod power cell: that is prefab data the IL does not show, and no number for it is
    ///     confirmed. Only a bound for refusing levels no cell can hold, so it is set far above any plausible capacity,
    ///     and <see cref="TryGetCapacity" /> knows no capacity for the cells.
    /// </summary>
    private const float ESCAPE_POD_POWER_BOUND = 1000f;

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

    /// <summary>
    ///     The highest level the server accepts for a source of this type: its capacity where that is taken to be known,
    ///     a generous bound otherwise (a Lifepod cell). Not for showing as a capacity; see <see cref="TryGetCapacity" />.
    /// </summary>
    public static bool TryGetMaxPower(BasePowerSourceType sourceType, out float maxPower)
    {
        maxPower = sourceType switch
        {
            BasePowerSourceType.ESCAPE_POD => ESCAPE_POD_POWER_BOUND,
            _ => TryGetCapacity(sourceType, out float capacity) ? capacity : 0f
        };
        return maxPower > 0f;
    }

    /// <summary>
    ///     The capacity of a source of this type, where it is taken to be known. False for a Lifepod cell.
    /// </summary>
    public static bool TryGetCapacity(BasePowerSourceType sourceType, out float capacity)
    {
        capacity = sourceType switch
        {
            BasePowerSourceType.SOLAR => SOLAR_MAX_POWER,
            BasePowerSourceType.THERMAL => THERMAL_MAX_POWER,
            BasePowerSourceType.BIOREACTOR => BIOREACTOR_MAX_POWER,
            BasePowerSourceType.NUCLEAR => NUCLEAR_MAX_POWER,
            _ => 0f
        };
        return capacity > 0f;
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
